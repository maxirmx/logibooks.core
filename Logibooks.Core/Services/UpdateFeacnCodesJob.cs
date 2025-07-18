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
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Linq;
using Quartz;
using Logibooks.Core.Data;

namespace Logibooks.Core.Services;

public class UpdateFeacnCodesJob(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IUpdateFeacnCodesService service,
    ILogger<UpdateFeacnCodesJob> logger) : IJob
{
    private readonly AppDbContext _db = db;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IUpdateFeacnCodesService _service = service;
    private readonly ILogger<UpdateFeacnCodesJob> _logger = logger;

    private static CancellationTokenSource? _prev;
    private static readonly object _lock = new();

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
            var cells = row.SelectNodes("th|td")?.Select(c => (HtmlEntity.DeEntitize(c.InnerText) ?? String.Empty).Trim()).ToArray();
            if (cells == null || cells.Length == 0) continue;
            yield return cells;
        }
    }

    private async Task<List<FeacnCodeRow>> ExtractAsync(CancellationToken token)
    {
        var result = new List<FeacnCodeRow>();
        var orders = await _db.FeacnOrders.AsNoTracking().Where(o => o.Url != null).OrderBy(o => o.Id).ToListAsync(token);

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

                    result.Add(new FeacnCodeRow(url, code, name, comment));
                }
            }
        }

        return result;
    }

    private record FeacnCodeRow(string Url, string Code, string Name, string Comment);

    public async Task Execute(IJobExecutionContext context)
    {
        CancellationTokenSource cts;
        lock (_lock)
        {
            _prev?.Cancel();
            cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            _prev = cts;
        }

        _logger.LogInformation("Executing UpdateFeacnCodesJob");
        try
        {
            var extracted = await ExtractAsync(cts.Token);
            _logger.LogInformation("Extracted {Count} FEACN rows", extracted.Count);
            await _service.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("UpdateFeacnCodesJob was cancelled");
        }
        finally
        {
            cts.Dispose();
            lock (_lock)
            {
                if (_prev == cts) _prev = null;
            }
        }
    }
}
