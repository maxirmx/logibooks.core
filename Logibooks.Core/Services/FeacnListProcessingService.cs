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

using System.Data;
using System.Globalization;
using ExcelDataReader;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Services;

public class FeacnListProcessingService(
    AppDbContext db,
    ILogger<FeacnListProcessingService> logger) : IFeacnListProcessingService
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<FeacnListProcessingService> _logger = logger;
    
    private record struct FeacnExcelRow(
        int Id,
        int? ChildId,
        int? NextId,
        int Level,
        string Code,
        string CodeEx,
        DateOnly? Date1,
        DateOnly? Date2,
        DateOnly? DatePrev,
        string? TextPrev,
        string Text,
        string? TextEx);

    public async Task<int> ProcessFeacnCodesFromExcelAsync(
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting FEACN codes processing from file: {FileName}", fileName);
        
        try
        {
            // Parse Excel file
            var excelRows = ParseExcelFile(content);
            _logger.LogInformation("Parsed {Count} rows from Excel file", excelRows.Count);
            
            // Build hierarchical structure and convert to FeacnCode entities
            var feacnCodes = BuildFeacnCodesFromExcelRows(excelRows);
            _logger.LogInformation("Built {Count} FEACN code entities", feacnCodes.Count);
            
            // Replace data in database using transaction
            var processedCount = await ReplaceFeacnCodesInDatabaseAsync(feacnCodes, cancellationToken);
            
            _logger.LogInformation("Successfully processed {Count} FEACN codes from file: {FileName}", 
                processedCount, fileName);
            
            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing FEACN codes from file: {FileName}", fileName);
            throw;
        }
    }

    private List<FeacnExcelRow> ParseExcelFile(byte[] content)
    {
        var rows = new List<FeacnExcelRow>();
        
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        
        using var stream = new MemoryStream(content);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet();
        
        if (dataSet.Tables.Count == 0)
            throw new InvalidOperationException("Excel file contains no data tables");
        
        var table = dataSet.Tables[0];
        if (table.Rows.Count < 2)
            throw new InvalidOperationException("Excel file must contain at least header row and one data row");
        
        // Validate header row
        ValidateHeaderRow(table);
        
        // Parse data rows (skip header row at index 0)
        for (int rowIndex = 1; rowIndex < table.Rows.Count; rowIndex++)
        {
            try
            {
                var row = ParseSingleRow(table.Rows[rowIndex], rowIndex + 1);
                if (row.HasValue)
                {
                    rows.Add(row.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse row {RowNumber}, skipping", rowIndex + 1);
            }
        }
        
        return rows;
    }

    private void ValidateHeaderRow(DataTable table)
    {
        var expectedHeaders = new[] { "ID", "Child", "Next", "Level", "Code", "CodeEx", 
            "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx", "Unit", "UnitCode" };
        
        if (table.Columns.Count < expectedHeaders.Length)
            throw new InvalidOperationException($"Excel file must contain at least {expectedHeaders.Length} columns");
        
        for (int i = 0; i < expectedHeaders.Length; i++)
        {
            var actualHeader = table.Rows[0][i]?.ToString()?.Trim() ?? "";
            if (!string.Equals(actualHeader, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Header mismatch at column {Column}: expected '{Expected}', got '{Actual}'", 
                    i + 1, expectedHeaders[i], actualHeader);
            }
        }
    }

    private FeacnExcelRow? ParseSingleRow(DataRow row, int rowNumber)
    {
        // Parse ID (required)
        var idValue = row[0]?.ToString()?.Trim() ?? "";
        if (string.IsNullOrEmpty(idValue) || !int.TryParse(idValue, out var id))
        {
            _logger.LogDebug("Skipping row {RowNumber}: invalid or missing ID", rowNumber);
            return null;
        }
        
        // Parse Child ID (optional)
        int? childId = null;
        var childValue = row[1]?.ToString()?.Trim() ?? "";
        if (!string.IsNullOrEmpty(childValue) && int.TryParse(childValue, out var childParsed))
        {
            childId = childParsed;
        }
        
        // Parse Next ID (optional)
        int? nextId = null;
        var nextValue = row[2]?.ToString()?.Trim() ?? "";
        if (!string.IsNullOrEmpty(nextValue) && int.TryParse(nextValue, out var nextParsed))
        {
            nextId = nextParsed;
        }
        
        // Parse Level (required)
        var levelValue = row[3]?.ToString()?.Trim() ?? "";
        if (!int.TryParse(levelValue, out var level))
        {
            _logger.LogWarning("Row {RowNumber}: invalid level value '{Level}', using 0", rowNumber, levelValue);
            level = 0;
        }
        
        // Parse Code
        var code = TruncateWithWarning(row[4]?.ToString()?.Trim() ?? "", FeacnCode.FeacnCodeLength, 
            "Code", rowNumber);
        
        // Parse CodeEx
        var codeEx = TruncateWithWarning(row[5]?.ToString()?.Trim() ?? "", FeacnCode.FeacnCodeLength, 
            "CodeEx", rowNumber);
        
        // Parse dates
        var date1 = ParseDateOnly(row[6]?.ToString()?.Trim(), "Date1", rowNumber);
        var date2 = ParseDateOnly(row[7]?.ToString()?.Trim(), "Date2", rowNumber);
        var datePrev = ParseDateOnly(row[8]?.ToString()?.Trim(), "DatePrev", rowNumber);
        
        // Parse text fields
        var textPrev = row[9]?.ToString()?.Trim();
        if (string.IsNullOrEmpty(textPrev)) textPrev = null;
        
        var text = row[10]?.ToString()?.Trim() ?? "";
        var textEx = row[11]?.ToString()?.Trim();
        if (string.IsNullOrEmpty(textEx)) textEx = null;
        
        // Unit and UnitCode are ignored as per requirements
        
        return new FeacnExcelRow(id, childId, nextId, level, code, codeEx, 
            date1, date2, datePrev, textPrev, text, textEx);
    }

    private string TruncateWithWarning(string value, int maxLength, string fieldName, int rowNumber)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        
        if (value.Length > maxLength)
        {
            _logger.LogWarning("Row {RowNumber}: {FieldName} value truncated from {OriginalLength} to {MaxLength} characters", 
                rowNumber, fieldName, value.Length, maxLength);
            return value.Substring(0, maxLength);
        }
        
        return value;
    }

    private DateOnly? ParseDateOnly(string? dateValue, string fieldName, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(dateValue))
            return null;
        
        // Try various date formats
        var formats = new[] { "yyyy-MM-dd", "dd.MM.yyyy", "MM/dd/yyyy", "dd/MM/yyyy" };
        
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateValue, format, CultureInfo.InvariantCulture, 
                DateTimeStyles.None, out var parsedDate))
            {
                return DateOnly.FromDateTime(parsedDate);
            }
        }
        
        // Try general parsing
        if (DateTime.TryParse(dateValue, out var generalParsedDate))
        {
            return DateOnly.FromDateTime(generalParsedDate);
        }
        
        _logger.LogWarning("Row {RowNumber}: Could not parse {FieldName} date value '{DateValue}'", 
            rowNumber, fieldName, dateValue);
        return null;
    }

    private List<FeacnCode> BuildFeacnCodesFromExcelRows(List<FeacnExcelRow> excelRows)
    {
        var feacnCodes = new List<FeacnCode>();
        var idToIndexMap = new Dictionary<int, int>();
        
        // First pass: create all FeacnCode entities
        for (int i = 0; i < excelRows.Count; i++)
        {
            var row = excelRows[i];
            idToIndexMap[row.Id] = i;
            
            var feacnCode = new FeacnCode
            {
                Code = row.Code,
                CodeEx = row.CodeEx,
                Description = row.Text,
                DescriptionEx = row.TextEx ?? "",
                FromDate = row.Date1,
                ToDate = row.Date2,
                OldNameToDate = row.DatePrev,
                OldName = row.TextPrev
            };
            
            feacnCodes.Add(feacnCode);
        }
        
        // Second pass: establish parent-child relationships
        for (int i = 0; i < excelRows.Count; i++)
        {
            var row = excelRows[i];
            
            if (row.ChildId.HasValue && idToIndexMap.TryGetValue(row.ChildId.Value, out var childIndex))
            {
                // This row is parent of the child
                feacnCodes[childIndex].Parent = feacnCodes[i];
                feacnCodes[childIndex].ParentId = null; // Will be set when saving to database
                
                // Initialize children collection if needed
                feacnCodes[i].Children ??= new List<FeacnCode>();
                
                feacnCodes[i].Children!.Add(feacnCodes[childIndex]);
            }
        }
        
        _logger.LogInformation("Built hierarchical structure with {RootCount} root nodes", 
            feacnCodes.Count(f => f.Parent == null));
        
        return feacnCodes;
    }

    private async Task<int> ReplaceFeacnCodesInDatabaseAsync(List<FeacnCode> feacnCodes, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            _logger.LogInformation("Starting database transaction to replace FEACN codes");
            
            // Clear existing data
            var existingCodes = await _db.FeacnCodes.ToListAsync(cancellationToken);
            if (existingCodes.Any())
            {
                _db.FeacnCodes.RemoveRange(existingCodes);
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Removed {Count} existing FEACN codes", existingCodes.Count);
            }
            
            // Add new data in batches
            var batchSize = 1000;
            var insertedCount = 0;
            
            for (int i = 0; i < feacnCodes.Count; i += batchSize)
            {
                var batch = feacnCodes.Skip(i).Take(batchSize).ToList();
                
                // Clear entity tracking to avoid conflicts
                foreach (var entity in batch)
                {
                    entity.Id = 0; // Let EF generate new IDs
                    entity.ParentId = null; // Will set parent relationships after first save
                }
                
                await _db.FeacnCodes.AddRangeAsync(batch, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                
                insertedCount += batch.Count;
                _logger.LogDebug("Inserted batch {BatchStart}-{BatchEnd}", i + 1, i + batch.Count);
            }
            
            // Now update parent relationships
            // TODO: Implement parent relationship updates if needed for hierarchical structure
            
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Successfully replaced FEACN codes table with {Count} records", insertedCount);
            
            return insertedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database transaction, rolling back");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}