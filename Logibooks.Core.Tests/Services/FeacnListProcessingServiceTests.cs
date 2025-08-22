// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class FeacnListProcessingServiceTests
{
    private AppDbContext _dbContext = null!;
    private FeacnListProcessingService _service = null!;
    private ILogger<FeacnListProcessingService> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        _logger = new LoggerFactory().CreateLogger<FeacnListProcessingService>();
        _service = new FeacnListProcessingService(_dbContext, _logger);
        _dbContext.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public void ServiceInstantiation_ShouldCreateSuccessfully()
    {
        Assert.That(_service, Is.Not.Null);
    }

    [Test]
    public void UploadFeacnCodesAsync_EmptyExcelFile_ShouldThrowInvalidOperationException()
    {
        var emptyExcelBytes = CreateEmptyExcelFile();
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.UploadFeacnCodesAsync(emptyExcelBytes, "empty.xlsx"));
    }

    [Test]
    public void UploadFeacnCodesAsync_MissingRequiredHeaders_ShouldThrowInvalidOperationException()
    {
        var headers = new[] { "ID", "Child" }; // missing others
        var rows = new object[][] { new object[] { 1, "" } };
        var excelBytes = CreateExcelFile((headers, rows));
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => _service.UploadFeacnCodesAsync(excelBytes, "bad.xlsx"));
        Assert.That(ex!.Message, Does.Contain("обязательные столбцы"));
    }

    [Test]
    public async Task UploadFeacnCodesAsync_ValidFile_ShouldProcessSuccessfully()
    {
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[][]
        {
            new object[] { 1, "", "", "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Desc", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));
        await _service.UploadFeacnCodesAsync(excelBytes, "codes.xlsx");
        Assert.That(_dbContext.FeacnCodes.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task UploadFeacnCodesAsync_HierarchicalStructure_ShouldSetParentChild()
    {
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[][]
        {
            new object[] { 1, 2, "", "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Parent", "" },
            new object[] { 2, "", "", "2000000000", "2000000000", "2024-01-01", "2024-12-31", "", "", "Child", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));
        await _service.UploadFeacnCodesAsync(excelBytes, "codes.xlsx");
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(2));
        var child = codes.First(c => c.Code == "2000000000");
        var parent = codes.First(c => c.Code == "1000000000");
        Assert.That(child.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public async Task UploadFeacnCodesAsync_NextColumn_ShouldLinkSiblings()
    {
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[][]
        {
            new object[] { 1, 2, "", "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Parent", "" },
            new object[] { 2, "", 3, "2000000000", "2000000000", "2024-01-01", "2024-12-31", "", "", "Child1", "" },
            new object[] { 3, "", "", "3000000000", "3000000000", "2024-01-01", "2024-12-31", "", "", "Child2", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));
        await _service.UploadFeacnCodesAsync(excelBytes, "codes.xlsx");
        var codes = _dbContext.FeacnCodes.ToList();
        var parent = codes.First(c => c.Code == "1000000000");
        var child1 = codes.First(c => c.Code == "2000000000");
        var child2 = codes.First(c => c.Code == "3000000000");
        Assert.That(child1.ParentId, Is.EqualTo(parent.Id));
        Assert.That(child2.ParentId, Is.EqualTo(parent.Id));
        Assert.That(parent.Children, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task UploadFeacnCodesAsync_NextColumn_ShouldHandleMultipleChains()
    {
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[][]
        {
            new object[] { 1, 10, "", "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Parent", "" },
            new object[] { 10, "", 20, "2000000000", "2000000000", "2024-01-01", "2024-12-31", "", "", "Child1", "" },
            new object[] { 20, 40, 30, "3000000000", "3000000000", "2024-01-01", "2024-12-31", "", "", "Child2", "" },
            new object[] { 30, "", 0, "4000000000", "4000000000", "2024-01-01", "2024-12-31", "", "", "Child3", "" },
            new object[] { 40, "", 50, "5000000000", "5000000000", "2024-01-01", "2024-12-31", "", "", "Grandchild1", "" },
            new object[] { 50, "", "", "6000000000", "6000000000", "2024-01-01", "2024-12-31", "", "", "Grandchild2", "" }
        };

        var excelBytes = CreateExcelFile((headers, rows));
        await _service.UploadFeacnCodesAsync(excelBytes, "codes.xlsx");

        var codes = _dbContext.FeacnCodes.ToList();
        var parent = codes.First(c => c.Code == "1000000000");
        var child1 = codes.First(c => c.Code == "2000000000");
        var child2 = codes.First(c => c.Code == "3000000000");
        var child3 = codes.First(c => c.Code == "4000000000");
        var grand1 = codes.First(c => c.Code == "5000000000");
        var grand2 = codes.First(c => c.Code == "6000000000");

        Assert.That(parent.Children, Has.Count.EqualTo(3));
        Assert.That(child1.ParentId, Is.EqualTo(parent.Id));
        Assert.That(child2.ParentId, Is.EqualTo(parent.Id));
        Assert.That(child3.ParentId, Is.EqualTo(parent.Id));
        Assert.That(child2.Children, Has.Count.EqualTo(2));
        Assert.That(grand1.ParentId, Is.EqualTo(child2.Id));
        Assert.That(grand2.ParentId, Is.EqualTo(child2.Id));
    }

    [Test]
    public async Task UploadFeacnCodesAsync_ReplaceData_ShouldRemoveOldAndInsertNew()
    {
        _dbContext.FeacnCodes.Add(new FeacnCode { Code = "old", CodeEx = "old", Name = "Old", NormalizedName = "OLD" });
        await _dbContext.SaveChangesAsync();
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[][] { new object[] { 1, "", "", "new", "new", "2024-01-01", "2024-12-31", "", "", "New", "" } };
        var excelBytes = CreateExcelFile((headers, rows));
        await _service.UploadFeacnCodesAsync(excelBytes, "codes.xlsx");
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("new"));
    }

    private static byte[] CreateExcelFile(params (string[] headers, object[][] rows)[] sheetData)
    {
        var ruCulture = new CultureInfo("ru-RU");
        using var ms = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());
            var sheets = new Sheets();
            var sheet = new Sheet()
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1"
            };
            sheets.Append(sheet);
            workbookPart.Workbook.Append(sheets);
            var (headers, rows) = sheetData[0];
            var sheetDataNode = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            var headerRow = new Row() { RowIndex = 1 };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = new Cell()
                {
                    CellReference = GetCellReference(1, i + 1),
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(headers[i]))
                };
                headerRow.Append(cell);
            }
            sheetDataNode!.Append(headerRow);
            for (int r = 0; r < rows.Length; r++)
            {
                var dataRow = new Row() { RowIndex = (uint)(r + 2) };
                for (int c = 0; c < headers.Length; c++)
                {
                    var value = c < rows[r].Length ? rows[r][c] : null;
                    var cellValue = "";
                    if (value is not null)
                    {
                        if (value is DateTime dt)
                            cellValue = dt.ToString("dd.MM.yyyy", ruCulture);
                        else
                            cellValue = value.ToString() ?? "";
                    }
                    var cell = new Cell()
                    {
                        CellReference = GetCellReference((uint)(r + 2), c + 1),
                        DataType = CellValues.InlineString,
                        InlineString = new InlineString(new Text(cellValue))
                    };
                    dataRow.Append(cell);
                }
                sheetDataNode.Append(dataRow);
            }
            workbookPart.Workbook.Save();
        }
        return ms.ToArray();
    }

    private static byte[] CreateEmptyExcelFile()
    {
        using var ms = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            workbookPart.Workbook.Save();
        }
        return ms.ToArray();
    }

    private static string GetCellReference(uint row, int column)
    {
        string columnName = "";
        while (column > 0)
        {
            column--;
            columnName = (char)('A' + column % 26) + columnName;
            column /= 26;
        }
        return columnName + row;
    }
}
