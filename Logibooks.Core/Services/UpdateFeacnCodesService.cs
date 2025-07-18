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

using HtmlAgilityPack;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Logibooks.Core.Services;

public class UpdateFeacnCodesService(
    AppDbContext db,
    ILogger<UpdateFeacnCodesService> logger,
    IHttpClientFactory httpClientFactory) : IUpdateFeacnCodesService
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<UpdateFeacnCodesService> _logger = logger;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    private const string SpecialUrl = "https://www.alta.ru/tamdoc/10sr0318/";

    private static readonly string[] SkipStarts =
    [
        "наименование товара",
        "код товара в соответствии с ТН ВЭД",
        "код в соответствии с ТН ВЭД",
        "код ТН ВЭД ЕАЭС",
        "ТН ВЭД ЕАЭС",
        "позиция исключена",
        "(позиция введена",
        "(введено постановлением правительства",
        "исключено",
    ];

    private static readonly string[][] SkipHeaders =
    [
        [
            "Систематическаягруппапойкилотермныхводныхживотных",
            "Наименованиеболезнейиихмеждународныйиндекс",
            "Переченьвидов,чувствительныхкболезням"
        ],
        [
            "Время(часов)",
            "Температура(C)"
        ],
        [
            "Время(часов)",
            "Температура(°C)"
        ]
    ];

    private static readonly Regex[] EditorialPatterns =
    [
        new Regex(
            @"\(\s*в\s+ред\.\s+решени[йя]\s+.+?\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bиз\s+группы?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"\bиз\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"Распространяется\s+на\s+правоотношения,\s+возникшие\s+с\s+.+?\s+г\.",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"Нов\.\s+ред\.\s+Решение\s+.+?\s+(ЕЭК|КТС)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"См\.\s+пред\.\s+ред\.\s+Решение\s+.+?\s+(ЕЭК|КТС)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"Начало\s+действия\s+редакции\s+.+?\s+г\.",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"Редакция\s+действует\s+до\s+.+?\s+\(включительно\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)
    ];


    private static bool ShouldSkip(string cell)
    {
        var text = cell.Trim().ToLowerInvariant();
        foreach (var s in SkipStarts)
        {
            var lowerS = s.ToLowerInvariant();
            if (text.StartsWith(lowerS)) return true;
        }
        return false;
    }

    private static bool ShouldSkipTable(List<string[]> rows, int cols)
    {
        if (rows.Count == 0) return false;

        var firstRow = rows[0];
        
        if (firstRow.Length == 3)
        {
            foreach (var skipHeader in SkipHeaders)
            {
                if (skipHeader.Length == 3)
                {
                    Console.WriteLine($"Checking header: {firstRow[0]}");
                    if (firstRow[0].Trim().Replace(" ", "").Equals(skipHeader[0], StringComparison.OrdinalIgnoreCase) &&
                        firstRow[1].Trim().Replace(" ", "").Equals(skipHeader[1], StringComparison.OrdinalIgnoreCase) &&
                        firstRow[2].Trim().Replace(" ", "").Equals(skipHeader[2], StringComparison.OrdinalIgnoreCase)) 
                    {
                        return true;
                    }
                }
            }
        }

        else if (firstRow.Length == 2)
         {
             foreach (var skipHeader in SkipHeaders)
             {
                 if (skipHeader.Length == 2 &&
                     firstRow[0].Trim().Replace(" ", "").Equals(skipHeader[0], StringComparison.OrdinalIgnoreCase) &&
                     firstRow[1].Trim().Replace(" ", "").Equals(skipHeader[1], StringComparison.OrdinalIgnoreCase))
                 {
                     return true;
                 }
             }
         }
        return false;
    }

    private static string RemovePatterns(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;

        var cleanedCode = code;

        // Remove all instances of EditorialPatterns
        foreach (var pattern in EditorialPatterns)
        {
            cleanedCode = pattern.Replace(cleanedCode, string.Empty);
        }

        return cleanedCode.Trim();
    }
    private static IEnumerable<string[]> ParseTable(HtmlNode table)
    {
        var rows = table.SelectNodes(".//tr");
        if (rows == null) yield break;
        foreach (var row in rows)
        {
            var cells = ExtractCells(row);
            if (cells == null || cells.Length == 0) continue;
            yield return cells;
        }
    }

    private static string[]? ExtractCells(HtmlNode row)
    {
        return row.SelectNodes("th|td")
            ?.Select(c => (HtmlEntity.DeEntitize(c.InnerText) ?? string.Empty).Trim())
            .ToArray();
    }
    private async Task<List<FeacnCodeRow>> ExtractAsync(CancellationToken token)
    {
        var result = new List<FeacnCodeRow>();
        var orders = await _db.FeacnOrders
            .AsNoTracking()
            .Where(o => o.Url != null)
            .OrderBy(o => o.Id)
            .ToListAsync(token);

        var client = _httpClientFactory.CreateClient();

        foreach (var order in orders)
        {
            if (order.Url is null) continue;

            string url = order.Url;
            string html;
            try
            {
                html = await client.GetStringAsync(url, token);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to download {Url}", url);
                continue;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var tables = doc.DocumentNode.SelectNodes("//table");
            if (tables == null) continue;

            foreach (var table in tables)
            {
                var rows = ParseTable(table).ToList();
                if (rows.Count == 0) continue;
                int cols = rows.Max(r => r.Length);
                if (cols < 2 || cols > 3) continue;


                if (ShouldSkipTable(rows, cols))
                {
                    _logger.LogInformation("Skipping table in {Url} due to known headers", url);
                    continue;
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    var cells = rows[i];
                    
                    if (cells.All(string.IsNullOrWhiteSpace)) continue;
                    if (cells.Any(ShouldSkip)) continue;

                    string code, name, comment = string.Empty;

                    if (cells.Length == 1) continue;

                    if (url == SpecialUrl)
                    {
                        code = cells.Length > 1 ? cells[1] : string.Empty;
                        name = cells[0];
                        if (cells.Length > 2) comment = cells[2];
                    }
                    else
                    {
                        code = cells[0];
                        name = cells.Length > 1 ? cells[1] : string.Empty;
                        if (cells.Length > 2) comment = cells[2];
                    }

                    code = RemovePatterns(code);

                    // Skip if code becomes empty after cleaning
                    if (string.IsNullOrWhiteSpace(code)) continue;

                    result.Add(new FeacnCodeRow(order.Id, code, name, comment));
                }
            }
        }

        return result;
    }

    private record FeacnCodeRow(int OrderId, string Code, string Name, string Comment);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading FEACN tables");
        var extracted = await ExtractAsync(cancellationToken);
        _logger.LogInformation("Extracted {Count} FEACN rows", extracted.Count);

        if (extracted.Count == 0)
        {
            _logger.LogInformation("No FEACN rows to process");
            return;
        }

        // Batch operation: fetch all existing prefixes in one query to avoid N+1 queries
        var orderIds = extracted.Select(r => r.OrderId).Distinct().ToList();
        var existingPrefixes = await _db.FeacnPrefixes
            .Where(p => orderIds.Contains(p.FeacnOrderId))
            .ToListAsync(cancellationToken);

        // Use a transaction for atomicity and better performance
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Remove all existing prefixes for these orders
            if (existingPrefixes.Count > 0)
            {
                _db.FeacnPrefixes.RemoveRange(existingPrefixes);
                _logger.LogInformation("Removing {Count} existing FEACN prefixes", existingPrefixes.Count);
            }

            // Add new prefixes using batch operation
            var newPrefixes = extracted.Select(row => new FeacnPrefix
            {
                FeacnOrderId = row.OrderId,
                Code = row.Code,
                Description = row.Name,
                Comment = row.Comment
            }).ToList();

            _db.FeacnPrefixes.AddRange(newPrefixes);
            _logger.LogInformation("Adding {Count} new FEACN prefixes", newPrefixes.Count);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Successfully updated FEACN prefixes for {OrderCount} orders", orderIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating FEACN prefixes, rolling back transaction");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
