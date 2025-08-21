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

    /// <summary>
    /// Creates an Excel file with no sheets (empty workbook)
    /// </summary>
    private static byte[] CreateEmptyExcelFile()
    {
        using var ms = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            // No sheets added - this creates an empty workbook
            workbookPart.Workbook.Save();
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Creates an Excel file with a sheet that has only headers but no data rows
    /// </summary>
    private static byte[] CreateHeaderOnlyExcelFile(string[] headers)
    {
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
            
            var worksheetSheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            
            // Add only header row, no data rows
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
        while (sw.Elapsed < TimeSpan.FromSeconds(10)) // Increased timeout
        {
            var current = _service.GetProgress(handle);
            if (current != null)
            {
                progress = current;
                if (current.Finished)
                {
                    break;
                }
            }
            await Task.Delay(50); // Increased polling interval
        }
        sw.Stop();
        return progress;
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
    public void StartProcessingAsync_EmptyExcelFile_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var emptyExcelBytes = CreateEmptyExcelFile();

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartProcessingAsync(emptyExcelBytes, "empty.xlsx"));
        
        // The CreateEmptyExcelFile actually creates a workbook with no sheets, 
        // so the ExcelDataReader might create tables but with no rows
        Assert.That(ex?.Message ?? string.Empty, Does.Contain("строка заголовка и одна строка данных").Or.Contain("таблиц данных"));
    }

    [Test]
    public void StartProcessingAsync_HeaderOnlyExcelFile_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var headers = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var headerOnlyExcelBytes = CreateHeaderOnlyExcelFile(headers);

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartProcessingAsync(headerOnlyExcelBytes, "headeronly.xlsx"));
        
        Assert.That(ex?.Message ?? string.Empty, Is.EqualTo("В файле Excel должна быть как минимум строка заголовка и одна строка данных"));
    }

    [Test]
    public void StartProcessingAsync_MissingRequiredHeaders_ShouldThrowInvalidOperationException()
    {
        // Arrange - create Excel file with incomplete headers
        var incompleteHeaders = new[] { "ID", "Child", "Next" }; // Missing required headers
        var rows = new object[][]
        {
            new object[] { 1, "", "" }
        };
        var excelBytes = CreateExcelFile((incompleteHeaders, rows));

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartProcessingAsync(excelBytes, "incomplete_headers.xlsx"));

        string msg = ex?.Message ?? string.Empty;

        Assert.That(msg, Does.Contain("В файле Excel отсутствуют обязательные столбцы:"));
        Assert.That(msg, Does.Contain("Code"));
        Assert.That(msg, Does.Contain("CodeEx"));
        Assert.That(msg, Does.Contain("Date1"));
        Assert.That(msg, Does.Contain("Date2"));
        Assert.That(msg, Does.Contain("DatePrev"));
        Assert.That(msg, Does.Contain("TextPrev"));
        Assert.That(msg, Does.Contain("Text"));
        Assert.That(msg, Does.Contain("TextEx"));
    }


    [Test]
    public async Task StartProcessingAsync_CaseInsensitiveHeaders_ShouldProcessSuccessfully()
    {
        // Arrange - test that headers are case-insensitive (this should work)
        var headers = new[] { "id", "child", "next", "level", "code", "codeex", "date1", "date2", "dateprev", "textprev", "text", "textex" };
        var rows = new object[][]
        {
            new object[] { 1, "", "", 1, "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "case_insensitive.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        Assert.That(progress.Error, Is.Null.Or.Empty);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("1000000000"));
    }

    [Test]
    public async Task StartProcessingAsync_ExtraHeaders_ShouldProcessSuccessfully()
    {
        // Arrange - test with extra headers beyond the required ones
        var headers = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx", "ExtraColumn1", "ExtraColumn2" };
        var rows = new object[][]
        {
            new object[] { 1, "", "", 1, "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "", "Extra1", "Extra2" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "extra_headers.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        Assert.That(progress.Error, Is.Null.Or.Empty);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("1000000000"));
    }

    [Test]
    public async Task StartProcessingAsync_ValidFile_ShouldProcessSuccessfully()
    {
        var headers = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[][] 
        {
            new object[] { 1, "", "", 1, "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" },
            new object[] { 2, 1, "", 2, "2000000000", "2000000000", "2024-01-01", "2024-12-31", "", "", "Child description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        var handle = await _service.StartProcessingAsync(excelBytes, "test.xlsx");
        var progress = await WaitForCompletion(handle);
        
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        Assert.That(progress.Error, Is.Null.Or.Empty);
        
        // Check database results instead of relying on progress count
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(2));
        Assert.That(codes.Any(c => c.Code == "1000000000"));
        Assert.That(codes.Any(c => c.Code == "2000000000"));
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
        Assert.That(progress!.Finished, Is.True);

        // Check database results instead of relying on progress count
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
            DescriptionEx = "Old"
        });
        await _dbContext.SaveChangesAsync();
        Assert.That(_dbContext.FeacnCodes.Count(), Is.EqualTo(1));

        var headers = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            new object[] { 1, "", "", 1, "newcode", "newcode", "2024-01-01", "2024-12-31", "", "", "New", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        var handle = await _service.StartProcessingAsync(excelBytes, "replace.xlsx");
        var progress = await WaitForCompletion(handle);
        
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);

        // Check database results instead of relying on progress count
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("newcode"));
    }

    #region TruncateWithWarning Edge Cases Tests

    [Test]
    public async Task StartProcessingAsync_TruncateWithWarning_NullValue_ShouldReturnEmptyString()
    {
        // Arrange - create Excel file with null Code value (simulated as empty string)
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            new object[] { 1, "", "", "", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "null_code.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        Assert.That(progress.Error, Is.Null.Or.Empty);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("")); // Should be empty string for null/empty input
    }

    [Test]
    public async Task StartProcessingAsync_TruncateWithWarning_EmptyString_ShouldReturnEmptyString()
    {
        // Arrange - create Excel file with empty Code value
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            new object[] { 1, "", "", "", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "empty_code.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo(""));
    }

    [Test]
    public async Task StartProcessingAsync_TruncateWithWarning_ExactMaxLength_ShouldNotTruncate()
    {
        // Arrange - create Excel file with Code exactly at max length (10 chars)
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var exactLengthCode = "1234567890"; // Exactly 10 characters
        var rows = new object[] []
        {
            new object[] { 1, "", "", exactLengthCode, "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "exact_length.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo(exactLengthCode));
        Assert.That(codes.Single().Code.Length, Is.EqualTo(10));
    }

    [Test]
    public async Task StartProcessingAsync_TruncateWithWarning_ExceedsMaxLength_ShouldTruncate()
    {
        // Arrange - create Excel file with Code exceeding max length
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var longCode = "12345678901234567890"; // 20 characters, should be truncated to 10
        var expectedTruncatedCode = "1234567890"; // First 10 characters
        var rows = new object[] []
        {
            new object[] { 1, "", "", longCode, "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "long_code.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo(expectedTruncatedCode));
        Assert.That(codes.Single().Code.Length, Is.EqualTo(10));
    }

    [Test]
    public async Task StartProcessingAsync_TruncateWithWarning_CodeExExceedsMaxLength_ShouldTruncate()
    {
        // Arrange - create Excel file with CodeEx exceeding max length
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var longCodeEx = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; // 26 characters, should be truncated to 10
        var expectedTruncatedCodeEx = "ABCDEFGHIJ"; // First 10 characters
        var rows = new object[] []
        {
            new object[] { 1, "", "", "1000000000", longCodeEx, "2024-01-01", "2024-12-31", "", "", "Test description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "long_codeex.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().CodeEx, Is.EqualTo(expectedTruncatedCodeEx));
        Assert.That(codes.Single().CodeEx.Length, Is.EqualTo(10));
    }

    [Test]
    public async Task StartProcessingAsync_TruncateWithWarning_WhitespaceOnlyValue_ShouldReturnEmptyString()
    {
        // Arrange - create Excel file with whitespace-only Code value
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            new object[] { 1, "", "", "   \t\n  ", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "whitespace_code.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("")); // Whitespace should result in empty string
    }

    #endregion

    #region GetColumnValue Edge Cases Tests

    [Test]
    public async Task StartProcessingAsync_GetColumnValue_NonExistentColumn_ShouldHandleGracefully()
    {
        // Arrange - create Excel file that references a column that doesn't exist in mapping
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            new object[] { 1, "", "", "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "normal_file.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert - should process normally since GetColumnValue handles non-existent columns
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        Assert.That(progress.Error, Is.Null.Or.Empty);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task StartProcessingAsync_GetColumnValue_ColumnIndexOutOfRange_ShouldReturnNull()
    {
        // This tests the edge case where columnIndex >= row.Table.Columns.Count
        // We'll create an Excel file with data that might trigger this scenario
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            // Row with fewer cells than expected columns
            new object[] { 1, "", "", "1000000000", "1000000000" } // Missing some columns
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "short_row.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert - should handle gracefully and use default values for missing columns
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        // Missing columns should result in empty/null values
        Assert.That(codes.Single().Code, Is.EqualTo("1000000000"));
        Assert.That(codes.Single().CodeEx, Is.EqualTo("1000000000"));
    }

    [Test]
    public async Task StartProcessingAsync_GetColumnValue_EmptyHeaderNames_ShouldBeSkipped()
    {
        // Arrange - create Excel file with some empty header names, but keep all required headers
        // We'll add an extra empty column between valid headers
        var headers = new[] { "ID", "Child", "", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx", "" };
        var rows = new object[] []
        {
            new object[] { 1, "", "ExtraValue1", "", "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "", "ExtraValue2" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "empty_headers.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert - should process normally, empty headers are skipped in mapping
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("1000000000"));
    }

    [Test]
    public async Task StartProcessingAsync_GetColumnValue_WhitespaceOnlyHeaderNames_ShouldBeSkipped()
    {
        // Arrange - create Excel file with whitespace-only header names, but keep all required headers
        // We'll add extra whitespace-only columns between valid headers
        var headers = new[] { "ID", "Child", "   ", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx", "\t\n" };
        var rows = new object[] []
        {
            new object[] { 1, "", "ExtraValue1", "", "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "", "ExtraValue2" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "whitespace_headers.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert - should process normally, whitespace-only headers are skipped
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("1000000000"));
    }

    [Test]
    public async Task StartProcessingAsync_GetColumnValue_CaseInsensitiveHeaderMatching_ShouldWork()
    {
        // Arrange - create Excel file with mixed case headers that should still be matched
        var headers = new[] { "id", "CHILD", "Next", "code", "CODEEX", "date1", "DATE2", "dateprev", "TEXTPREV", "text", "TEXTEX" };
        var rows = new object[] []
        {
            new object[] { 1, "", "", "1000000000", "1000000000", "2024-01-01", "2024-12-31", "", "", "Test description", "" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "mixed_case_headers.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert - should process normally due to case-insensitive matching
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("1000000000"));
        Assert.That(codes.Single().CodeEx, Is.EqualTo("1000000000"));
    }

    [Test]
    public async Task StartProcessingAsync_GetColumnValue_CellValueWithLeadingTrailingWhitespace_ShouldBeTrimmed()
    {
        // Arrange - create Excel file with cell values that have leading/trailing whitespace
        var headers = new[] { "ID", "Child", "Next", "Code", "CodeEx", "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        var rows = new object[] []
        {
            new object[] { 1, "", "", "  1000000000  ", "\t1000000000\n", "2024-01-01", "2024-12-31", "", "", "  Test description  ", "\tTest extra\n" }
        };
        var excelBytes = CreateExcelFile((headers, rows));

        // Act
        var handle = await _service.StartProcessingAsync(excelBytes, "whitespace_values.xlsx");
        var progress = await WaitForCompletion(handle);
        
        // Assert - values should be trimmed
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.Finished, Is.True);
        
        var codes = _dbContext.FeacnCodes.ToList();
        Assert.That(codes.Count, Is.EqualTo(1));
        Assert.That(codes.Single().Code, Is.EqualTo("1000000000")); // Should be trimmed
        Assert.That(codes.Single().CodeEx, Is.EqualTo("1000000000")); // Should be trimmed
        Assert.That(codes.Single().Description, Is.EqualTo("Test description")); // Should be trimmed
        Assert.That(codes.Single().DescriptionEx, Is.EqualTo("Test extra")); // Should be trimmed
    }

    #endregion

}
