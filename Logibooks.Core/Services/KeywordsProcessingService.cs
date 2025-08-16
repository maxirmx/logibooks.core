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

using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using ExcelDataReader;

using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;

namespace Logibooks.Core.Services;

public class KeywordsProcessingService(AppDbContext db, ILogger<KeywordsProcessingService> logger) : IKeywordsProcessingService
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<KeywordsProcessingService> _logger = logger;
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
            for (int c = 0; c < table.Columns.Count; c++)
            {
                var head = header[c]?.ToString()?.Trim().ToLower(RussianCulture);
                if (head == "код")
                    codeCol = c;
                else if (head == "наименование")
                    nameCol = c;
            }

            if (codeCol < 0 || nameCol < 0)
                throw new InvalidOperationException("Не найдены столбцы 'код' и 'наименование'");

            var parsed = new List<KeyWord>();
            for (int r = 1; r < table.Rows.Count; r++)
            {
                var codeValue = table.Rows[r][codeCol];
                var code = codeValue?.ToString()?.Trim() ?? string.Empty;
                
                var name = table.Rows[r][nameCol]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                    continue;

                if (!NineOrTenDigitCodeRegex.IsMatch(code))
                    throw new InvalidOperationException($"Код '{code}' в строке {r + 1} должен содержать ровно 9 или 10 цифр");

                // Prepend zero if code has 9 digits
                if (code.Length == 9)
                    code = "0" + code;

                // Squash duplicate words in a line
                var words = name
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Trim())
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .Select(w => w.ToLower(RussianCulture))
                    .Distinct();

                foreach (var word in words)
                {
                    int matchType = word.Any(char.IsWhiteSpace)
                        ? (int)WordMatchTypeCode.Phrase
                        : (int)WordMatchTypeCode.WeakMorphology;

                    parsed.Add(new KeyWord
                    {
                        Word = word,
                        FeacnCode = code,
                        MatchTypeId = matchType
                    });
                }
            }

            // Squash duplicate entries in parsed if all fields are equal
            parsed = parsed
                .GroupBy(k => new { k.Word, k.FeacnCode, k.MatchTypeId })
                .Select(g => g.First())
                .ToList();

            var duplicateWords = parsed
                .GroupBy(k => k.Word.ToLower(RussianCulture))
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if ( duplicateWords.Count > 0)
            {
                string dupWordsString = string.Join(", ", duplicateWords);
                _logger.LogError("Ключевые слова и фразы заданы более одного раза: {d}", dupWordsString);
                throw new InvalidOperationException("Ключевые слова и фразы заданы более одного раза: " + dupWordsString);

            }

            if (_db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                await ProcessKeywords(parsed, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            else
            {
                await ProcessKeywords(parsed, cancellationToken);
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
        var existing = await _db.KeyWords.ToListAsync(cancellationToken);
        var existingDict = existing.ToDictionary(k => k.Word.ToLower(RussianCulture), k => k);
        var incomingWords = new HashSet<string>(parsed.Select(p => p.Word), StringComparer.OrdinalIgnoreCase);

        foreach (var kw in parsed)
        {
            if (existingDict.TryGetValue(kw.Word, out var existingKw))
            {
                existingKw.FeacnCode = kw.FeacnCode;
                existingKw.MatchTypeId = kw.MatchTypeId;
                _db.KeyWords.Update(existingKw);
            }
            else
            {
                _db.KeyWords.Add(kw);
            }
        }

        foreach (var kw in existing)
        {
            if (!incomingWords.Contains(kw.Word))
                _db.KeyWords.Remove(kw);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

