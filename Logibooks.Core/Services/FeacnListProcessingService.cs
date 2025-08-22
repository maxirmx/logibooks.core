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
using ExcelDataReader;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Services;


// Этот сервис отвечает за обработку и загрузку кодов ТН ВЭД из Excel-файла
// Он сознательно блокирует возможность паралелльной обработки.
// Паралелльная загрузка -  это не жизненный сценарий, а возможный результат
// хаотичного нажатия кнопок несколькиими операторами 


public class FeacnListProcessingService(
    AppDbContext db,
    ILogger<FeacnListProcessingService> logger) : IFeacnListProcessingService
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<FeacnListProcessingService> _logger = logger;
    private static bool _isProcessing;
    private static readonly object _processLock = new();
    
    private record struct FeacnExcelRow(
        int Id,
        int? ChildId,
        string Code,
        string CodeEx,
        DateOnly? Date1,
        DateOnly? Date2,
        DateOnly? DatePrev,
        string? TextPrev,
        string Text,
        string? TextEx);

    public async Task UploadFeacnCodesAsync(
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        lock (_processLock)
        {
            if (_isProcessing)
            {
                throw new InvalidOperationException("Загрузка кодов ТН ВЭД уже выполняется.");
            }
            _isProcessing = true;
        }

        try
        {
            _logger.LogInformation("Starting FEACN codes processing from file: {FileName}", fileName);

            // Parse and validate the Excel file
            var excelRows = ParseExcelFile(content);
            _logger.LogInformation("Parsed {Count} rows from Excel file", excelRows.Count);

            // Build the code hierarchy
            var feacnCodes = BuildFeacnCodesFromExcelRows(excelRows);
            _logger.LogInformation("Built {Count} FEACN code entities", feacnCodes.Count);

            // Perform the actual database replacement
            await ReplaceFeacnCodesInDatabaseAsync(feacnCodes, cancellationToken);

            _logger.LogInformation("Successfully processed FEACN codes from file: {FileName}", fileName);
        }
        finally
        {
            lock (_processLock)
            {
                _isProcessing = false;
            }
        }
    }

    private List<FeacnExcelRow> ParseExcelFile(byte[] content)
    {
        var rows = new List<FeacnExcelRow>();
        
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        
        using var stream = new MemoryStream(content);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet();
        
   
        var table = dataSet.Tables.Count > 0 ? dataSet.Tables[0] : null;
        if (table == null || table.Rows.Count < 2)
            throw new InvalidOperationException("В файле Excel должна быть как минимум строка заголовка и одна строка данных");
        
        // Validate header row and get column mapping
        var columnMap = ValidateHeaderRow(table);
        
        // Parse data rows (skip header row at index 0)
        for (int rowIndex = 1; rowIndex < table.Rows.Count; rowIndex++)
        {
            try
            {
                var row = ParseSingleRow(table.Rows[rowIndex], columnMap, rowIndex + 1);
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

    private Dictionary<string, int> ValidateHeaderRow(DataTable table)
    {
        var requiredHeaders = new[] { "ID", "Child", "Next", "Code", "CodeEx", 
            "Date1", "Date2", "DatePrev", "TextPrev", "Text", "TextEx" };
        
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        // Map header names to column indices
        for (int i = 0; i < table.Columns.Count; i++)
        {
            var headerName = table.Rows[0][i]?.ToString()?.Trim() ?? "";
            if (!string.IsNullOrEmpty(headerName))
            {
                columnMap[headerName] = i;
            }
        }
        
        // Check for required headers
        var missingHeaders = new List<string>();
        foreach (var header in requiredHeaders)
        {
            if (!columnMap.ContainsKey(header))
            {
                missingHeaders.Add(header);
            }
        }
        
        if (missingHeaders.Any())
        {
            throw new InvalidOperationException($"В файле Excel отсутствуют обязательные столбцы: {string.Join(", ", missingHeaders)}");
        }
        
        _logger.LogInformation("Successfully mapped {Count} columns from Excel headers", columnMap.Count);
        return columnMap;
    }

    private FeacnExcelRow? ParseSingleRow(DataRow row, Dictionary<string, int> columnMap, int rowNumber)
    {
        // Parse ID (required)
        var idValue = GetColumnValue(row, columnMap, "ID");
        var id = (int?)ExcelDataConverter.ConvertValueToPropertyType(idValue, typeof(int), "ID", _logger);
        if (!id.HasValue || id.Value == 0)
        {
            _logger.LogDebug("Skipping row {RowNumber}: invalid or missing ID", rowNumber);
            return null;
        }
        
        // Parse Child ID (optional)
        var childValue = GetColumnValue(row, columnMap, "Child");
        var childId = (int?)ExcelDataConverter.ConvertValueToPropertyType(childValue, typeof(int?), "Child", _logger);
        
        // Parse Next ID (optional)
        // var nextValue = GetColumnValue(row, columnMap, "Next");
        // var nextId = (int?)ExcelDataConverter.ConvertValueToPropertyType(nextValue, typeof(int?), "Next", _logger);
        
        // Parse Level (required)
        // var levelValue = GetColumnValue(row, columnMap, "Level");
        // var level = (int?)ExcelDataConverter.ConvertValueToPropertyType(levelValue, typeof(int), "Level", _logger) ?? 0;
        
        // Parse Code
        var codeValue = GetColumnValue(row, columnMap, "Code");
        var code = TruncateWithWarning(codeValue, FeacnCode.FeacnCodeLength, "Code", rowNumber);

        var codeEx = GetColumnValue(row, columnMap, "CodeEx");

        // Parse dates using the converter helper
        var date1Value = GetColumnValue(row, columnMap, "Date1");
        var date1 = (DateOnly?)ExcelDataConverter.ConvertValueToPropertyType(date1Value, typeof(DateOnly?), "Date1", _logger);
        
        var date2Value = GetColumnValue(row, columnMap, "Date2");
        var date2 = (DateOnly?)ExcelDataConverter.ConvertValueToPropertyType(date2Value, typeof(DateOnly?), "Date2", _logger);
        
        var datePrevValue = GetColumnValue(row, columnMap, "DatePrev");
        var datePrev = (DateOnly?)ExcelDataConverter.ConvertValueToPropertyType(datePrevValue, typeof(DateOnly?), "DatePrev", _logger);

        // Parse text fields

        var textPrev = GetColumnValue(row, columnMap, "TextPrev");
        if (string.IsNullOrEmpty(textPrev)) textPrev = null;
        
        var text = GetColumnValue(row, columnMap, "Text");
        
        var textEx = GetColumnValue(row, columnMap, "TextEx");
        if (string.IsNullOrEmpty(textEx)) textEx = null;
        
        // Unit and UnitCode are ignored
        
        return new FeacnExcelRow(id.Value, childId, code, codeEx, 
            date1, date2, datePrev, textPrev, text, textEx);
    }
    
    private static string GetColumnValue(DataRow row, Dictionary<string, int> columnMap, string columnName)
    {
        if (columnMap.TryGetValue(columnName, out int columnIndex) && columnIndex < row.Table.Columns.Count)
        {
            return row[columnIndex]?.ToString()?.Trim() ?? string.Empty;
        }
        return string.Empty;
    }

    private string TruncateWithWarning(string value, int maxLength, string fieldName, int rowNumber)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        
        if (value.Length > maxLength)
        {
            _logger.LogWarning("Row {RowNumber}: {FieldName} value truncated from {OriginalLength} to {MaxLength} characters", 
                rowNumber, fieldName, value.Length, maxLength);
            return value[..maxLength];
        }
        
        return value;
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
                Name = row.Text,
                NormalizedName = row.TextEx ?? "",
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
                feacnCodes[i].Children ??= [];
                
                feacnCodes[i].Children!.Add(feacnCodes[childIndex]);
            }
        }
        
        _logger.LogInformation("Построена иерархическая структура с {RootCount} корневыми узлами", 
            feacnCodes.Count(f => f.Parent == null));
        
        return feacnCodes;
    }

    private async Task<int> ReplaceFeacnCodesInDatabaseAsync(List<FeacnCode> feacnCodes, CancellationToken cancellationToken)
    {
        // Check if we're using in-memory database (doesn't support transactions)
        bool isInMemory = _db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        
        if (!isInMemory)
        {
            using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await ReplaceDataAsync(feacnCodes, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database transaction, rolling back");
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            // For in-memory database, just perform the operations without transaction
            _logger.LogInformation("Using in-memory database, skipping transaction");
            return await ReplaceDataAsync(feacnCodes, cancellationToken);
        }
    }

    private async Task<int> ReplaceDataAsync(List<FeacnCode> feacnCodes, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting FEACN codes replacement");
        
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
            }
            
            await _db.FeacnCodes.AddRangeAsync(batch, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            
            insertedCount += batch.Count;
            _logger.LogDebug("Inserted batch {BatchStart}-{BatchEnd}", i + 1, i + batch.Count);
        }
        
        _logger.LogInformation("Successfully replaced FEACN codes with {Count} records", insertedCount);
        return insertedCount;
    }

    // No progress reporting or cancellation in simplified implementation
}