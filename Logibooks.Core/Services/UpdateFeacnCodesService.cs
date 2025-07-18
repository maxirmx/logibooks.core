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
using System.Net.Http;
using System.Linq;

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
        "позиция исключена",
        "(позиция введена",
        "(введено постановлением правительства",
        "наименование товара"
    ];

    private static bool ShouldSkip(string cell)
    {
        var text = cell.Trim().ToLowerInvariant();
        foreach (var s in SkipStarts)
        {
            if (text.StartsWith(s)) return true;
        }
        return false;
    }

    private static IEnumerable<string[]> ParseTable(HtmlNode table)
    {
        var rows = table.SelectNodes(".//tr");
        if (rows == null) yield break;
        foreach (var row in rows)
        {
            var cells = row.SelectNodes("th|td")?.Select(c => (HtmlEntity.DeEntitize(c.InnerText) ?? string.Empty).Trim()).ToArray();
            if (cells == null || cells.Length == 0) continue;
            yield return cells;
        }
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
            var url = order.Url!;
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

                foreach (var cells in rows)
                {
                    if (cells.All(string.IsNullOrWhiteSpace)) continue;
                    if (cells.Any(ShouldSkip)) continue;

                    string code, name, comment = string.Empty;

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

        foreach (var grp in extracted.GroupBy(r => r.OrderId))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existing = await _db.FeacnPrefixes
                .Where(p => p.FeacnOrderId == grp.Key)
                .ToListAsync(cancellationToken);
            _db.FeacnPrefixes.RemoveRange(existing);

            foreach (var row in grp)
            {
                var prefix = new FeacnPrefix
                {
                    FeacnOrderId = grp.Key,
                    Code = row.Code,
                    Description = row.Name,
                    Comment = row.Comment
                };
                _db.FeacnPrefixes.Add(prefix);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
