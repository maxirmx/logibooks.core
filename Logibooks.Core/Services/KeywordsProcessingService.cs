// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using ExcelDataReader;

using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;

namespace Logibooks.Core.Services;

public class KeywordsProcessingService(AppDbContext db, ILogger<KeywordsProcessingService> logger, IMorphologySearchService morphologySearchService) : IKeywordsProcessingService
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<KeywordsProcessingService> _logger = logger;
    private readonly IMorphologySearchService _morphologySearchService = morphologySearchService;
    private static readonly CultureInfo RussianCulture = new("ru-RU");
    private static readonly Regex NineOrTenDigitCodeRegex = new("^[\\d]{9,10}$", RegexOptions.Compiled);

    public async Task<List<KeyWord>> UploadKeywordsFromExcelAsync(
        byte[] content,
        string fileName,    
        CancellationToken cancellationToken = default)
    {
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var ms = new MemoryStream(content);
            using var reader = ExcelReaderFactory.CreateReader(ms);
            var dataSet = reader.AsDataSet();
            if (dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count == 0)
                throw new InvalidOperationException("Файл не содержит данных");

            var table = dataSet.Tables[0];
            var header = table.Rows[0];
            int codeCol = -1;
            int nameCol = -1;
            int insertBeforeCol = -1;
            int insertAfterCol = -1;
            
            for (int c = 0; c < table.Columns.Count; c++)
            {
                var head = header[c]?.ToString()?.Trim().ToLower(RussianCulture);
                if (head == "код")
                    codeCol = c;
                else if (head == "наименование")
                    nameCol = c;
                else if (head == "перед описанием")
                    insertBeforeCol = c;
                else if (head == "в конце описания")
                    insertAfterCol = c;
            }

            if (codeCol < 0 || nameCol < 0)
                throw new InvalidOperationException("Не найдены столбцы 'код' и 'наименование'");

            var wordFeacnMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var feacnInsertItems = new Dictionary<string, FeacnInsertItem>();
            
            for (int r = 1; r < table.Rows.Count; r++)
            {
                var codeValue = table.Rows[r][codeCol];
                var code = codeValue?.ToString()?.Trim() ?? string.Empty;
                
                var name = table.Rows[r][nameCol]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                    continue;

                if (!NineOrTenDigitCodeRegex.IsMatch(code))
                    throw new InvalidOperationException($"Код '{code}' в строке {r + 1} должен содержать ровно 10 цифр");

                // Prepend zero if code has 9 digits
                if (code.Length == 9)
                    code = "0" + code;

                // Extract words from the name column
                var words = name
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Trim())
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .Select(w => w.ToLower(RussianCulture))
                    .Distinct();

                foreach (var word in words)
                {
                    if (!wordFeacnMap.TryGetValue(word, out var codes))
                    {
                        codes = new HashSet<string>();
                        wordFeacnMap[word] = codes;
                    }
                    codes.Add(code);
                }

                // Process FEACN insert items if columns are present
                if (insertBeforeCol >= 0 || insertAfterCol >= 0)
                {
                    string? insertBefore = null;
                    string? insertAfter = null;

                    if (insertBeforeCol >= 0)
                    {
                        var beforeValue = table.Rows[r][insertBeforeCol]?.ToString()?.Trim();
                        insertBefore = string.IsNullOrWhiteSpace(beforeValue) ? null : beforeValue;
                    }

                    if (insertAfterCol >= 0)
                    {
                        var afterValue = table.Rows[r][insertAfterCol]?.ToString()?.Trim();
                        insertAfter = string.IsNullOrWhiteSpace(afterValue) ? null : afterValue;
                    }

                    // Only create/update FEACN insert item if at least one value is not null
                    if (insertBefore != null || insertAfter != null)
                    {
                        feacnInsertItems[code] = new FeacnInsertItem
                        {
                            Code = code,
                            InsertBefore = insertBefore,
                            InsertAfter = insertAfter
                        };
                    }
                }
            }

            var parsed = new List<KeyWord>();
            foreach (var (word, codes) in wordFeacnMap)
            {
                int matchType;
                if (word.Any(char.IsWhiteSpace))
                {
                    matchType = (int)WordMatchTypeCode.Phrase;
                }
                else
                {
                    // Single word - check morphology support
                    var morphologySupport = _morphologySearchService.CheckWord(word);
                    matchType = morphologySupport == MorphologySupportLevel.NoSupport
                        ? (int)WordMatchTypeCode.ExactSymbols
                        : (int)WordMatchTypeCode.WeakMorphology;
                }

                var keyWord = new KeyWord
                {
                    Word = word,
                    MatchTypeId = matchType
                };

                keyWord.KeyWordFeacnCodes = codes.Select(code => new KeyWordFeacnCode
                {
                    FeacnCode = code,
                    KeyWord = keyWord
                }).ToList();

                parsed.Add(keyWord);
            }

            if (_db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                await ProcessKeywords(parsed, cancellationToken);
                await ProcessFeacnInsertItems(feacnInsertItems.Values, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            else
            {
                await ProcessKeywords(parsed, cancellationToken);
                await ProcessFeacnInsertItems(feacnInsertItems.Values, cancellationToken);
            }
            return parsed;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки файла {File}", fileName);
            throw new InvalidOperationException("Ошибка обработки файла ключевых слов", ex);
        }
    }

    private async Task ProcessKeywords(List<KeyWord> parsed, CancellationToken cancellationToken)
    {
        var existing = await _db.KeyWords
            .Include(k => k.KeyWordFeacnCodes)
            .ToListAsync(cancellationToken);

        var existingDict = existing.ToDictionary(k => k.Word.ToLower(RussianCulture), k => k, StringComparer.OrdinalIgnoreCase);

        foreach (var kw in parsed)
        {
            if (existingDict.TryGetValue(kw.Word, out var existingKw))
            {
                existingKw.MatchTypeId = kw.MatchTypeId;            
                var existingCodes = existingKw.KeyWordFeacnCodes.Select(fc => fc.FeacnCode).ToHashSet();
                
                foreach (var newCode in kw.KeyWordFeacnCodes)
                {
                    if (!existingCodes.Contains(newCode.FeacnCode))
                    {
                        existingKw.KeyWordFeacnCodes.Add(new KeyWordFeacnCode
                        {
                            KeyWordId = existingKw.Id,
                            FeacnCode = newCode.FeacnCode,
                            KeyWord = existingKw
                        });
                    }
                }
            }
            else
            {
                _db.KeyWords.Add(kw);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessFeacnInsertItems(IEnumerable<FeacnInsertItem> parsedItems, CancellationToken cancellationToken)
    {
        if (!parsedItems.Any())
            return;

        var codes = parsedItems.Select(item => item.Code).ToList();
        var existingItems = await _db.FeacnInsertItems
            .Where(item => codes.Contains(item.Code))
            .ToListAsync(cancellationToken);

        var existingDict = existingItems.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var newItem in parsedItems)
        {
            if (existingDict.TryGetValue(newItem.Code, out var existingItem))
            {
                // Update existing item - overwrite both fields
                existingItem.InsertBefore = newItem.InsertBefore;
                existingItem.InsertAfter = newItem.InsertAfter;
            }
            else
            {
                // Create new item
                _db.FeacnInsertItems.Add(newItem);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

