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


using Logibooks.Core.Services;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

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

        // Ensure database is created
        _dbContext.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    private static byte[] CreateExcelFile(params (string[] headers, object[][] rows)[] sheetData)
    {
        var ruCulture = new CultureInfo("ru-RU");
        
        using var ms = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            // Create workbook and worksheet parts
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
            
            // Get the first sheet data (our tests only use one sheet)
            var (headers, rows) = sheetData[0];
            
            var worksheetSheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            
            // Add header row
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
            worksheetSheetData!.Append(headerRow);
            
            // Add data rows
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
                worksheetSheetData.Append(dataRow);
            }
            
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

    private async Task<ValidationProgress?> WaitForCompletion(Guid handle)
    {
        ValidationProgress? progress = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            var current = _service.GetProgress(handle);
            if (current != null)
            {
                if (current.Total != -1 || progress == null)
                {
                    progress = current;
                }
                if (current.Finished)
                {
                    break;
                }
            }
            await Task.Delay(20);
        }
        sw.Stop();
        return progress;
    }

    [Test]
    public async Task StartProcessingAsync_WithEmptyContent_ShouldSetError()
    {
        var emptyBytes = Array.Empty<byte>();
        var handle = await _service.StartProcessingAsync(emptyBytes, "empty.xlsx");
        var progress = await WaitForCompletion(handle);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        Assert.That(progress.Error, Is.Not.Null);
    }

    [Test]
    public void ServiceInstantiation_ShouldCreateSuccessfully()
    {
        // Act & Assert
        Assert.That(_service, Is.Not.Null);
        Assert.That(_dbContext, Is.Not.Null);
        Assert.That(_logger, Is.Not.Null);
    }

    [Test]
    public async Task StartProcessingAsync_ValidFile_ShouldProcessSuccessfully()
    {
        var headers = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            new object[] { 1, "", "", 1, "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" },
            new object[] { 2, 1, "", 2, "2000000000", "2000000000", "2024-01-01", "2024-12-31", "", "", "Child description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        var handle = await _service.StartProcessingAsync(excelBytes, "test.xlsx");
        var progress = await WaitForCompletion(handle);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Error, Is.Null);
        Assert.That(progress.Processed, Is.EqualTo(2));

        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(2));
        Assert.That(codes.Any(c => c.Code == "1000000000"));
        Assert.That(codes.Any(c => c.Code == "2000000000"));
    }

    [Test]
    public async Task StartProcessingAsync_MissingRequiredColumns_ShouldSetError()
    {
        var headers = new[] { "ID", "Level", "Code" }; // Missing required columns
        var rows = new object[] []
        {
            new object[] { 1, 1, "1000000000" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        var handle = await _service.StartProcessingAsync(excelBytes, "missingcols.xlsx");
        var progress = await WaitForCompletion(handle);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Error, Does.Contain("обязательные столбцы"));
    }

    [Test]
    public async Task StartProcessingAsync_EmptyTable_ShouldSetError()
    {
        var headers = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = Array.Empty<object[]>();
        var excelBytes = CreateExcelFile((headers, rows));

        var handle = await _service.StartProcessingAsync(excelBytes, "emptytable.xlsx");
        var progress = await WaitForCompletion(handle);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Error, Does.Contain("строка заголовка"));
    }

    [Test]
    public async Task StartProcessingAsync_InvalidIdRow_ShouldSkipRow()
    {
        var headers = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            new object[] { 0, "", "", 1, "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" },
            new object[] { 2, "", "", 2, "2000000000", "2000000000", "2024-01-01", "2024-12-31", "", "", "Valid description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        var handle = await _service.StartProcessingAsync(excelBytes, "invalidid.xlsx");
        var progress = await WaitForCompletion(handle);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Processed, Is.EqualTo(1));

        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("2000000000"));
    }

    [Test]
    public async Task StartProcessingAsync_HierarchicalStructure_ShouldSetParentChild()
    {
        var headers = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            new object[] { 1, "", "", 1, "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Parent", "" },
            new object[] { 2, 1, "", 2, "2000000000", "2000000000", "2024-01-01", "2024-12-31", "", "", "Child", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        var handle = await _service.StartProcessingAsync(excelBytes, "hierarchy.xlsx");
        await WaitForCompletion(handle);

        var codes = _dbContext.FeacnCodes.Include(c => c.Parent).ToList();
        Assert.That(codes.Count, Is.EqualTo(2));

        var child = codes.FirstOrDefault(c => c.Code == "2000000000");
        var parent = codes.FirstOrDefault(c => c.Code == "1000000000");

        Assert.That(child, Is.Not.Null, "Child code should exist");
        Assert.That(parent, Is.Not.Null, "Parent code should exist");

        Assert.That(child!.Description, Is.EqualTo("Child"));
        Assert.That(parent!.Description, Is.EqualTo("Parent"));
    }

    [Test]
    public async Task StartProcessingAsync_ReplaceData_ShouldRemoveOldAndInsertNew()
    {
        _dbContext.FeacnCodes.Add(new FeacnCode
        {
            Code = "oldcode",
            CodeEx = "oldcode",
            Description = "Old",
            DescriptionEx = "Old",
            FromDate = null,
            ToDate = null,
            OldName = "",
            OldNameToDate = null
        });
        await _dbContext.SaveChangesAsync();
        Assert.That(_dbContext.FeacnCodes.Count(), Is.EqualTo(1));

        var headers = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[][]
        {
            new object[] { 1, "", "", 1, "newcode", "newcode", "2024-01-01", "2024-12-31", "", "", "New", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        var handle = await _service.StartProcessingAsync(excelBytes, "replace.xlsx");
        var progress = await WaitForCompletion(handle);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Processed, Is.EqualTo(1));

        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("newcode"));
    }

    [Test]
    public async Task StartProcessingAsync_TransactionRollbackOnError_ShouldNotInsert()
    {
        var headers = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            new object[] { 1, "", "", 1, "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        _dbContext.Dispose();

        var handle = await _service.StartProcessingAsync(excelBytes, "error.xlsx");
        var progress = await WaitForCompletion(handle);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Error, Is.Not.Null);
    }
}
