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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Logibooks.Core.Models;

namespace Logibooks.Core.Services;

public static partial class AltaParser
{
    [GeneratedRegex(@"(\d{4}\s\d{2}\s\d{3}\s\d)", RegexOptions.Compiled)]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"(\d{3})/?$")]
    private static partial Regex UrlNumberPattern();

    [GeneratedRegex(@".*?\(")]
    private static partial Regex ExceptionCodePrefix();

    [GeneratedRegex(@"[^\d,]+")]
    private static partial Regex NonDigitComma();

    private static readonly HttpClient SharedHttpClient = new HttpClient();

    public static async Task<(List<AltaItem> Items, List<AltaException> Exceptions)> ParseAsync(IEnumerable<string> urls, HttpClient? client = null)
    {
        var http = client ?? SharedHttpClient;
        var items = new List<AltaItem>();
        var exceptions = new List<AltaException>();

        foreach (var url in urls)
        {
            var html = await http.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var tables = doc.DocumentNode.SelectNodes("//table");
            if (tables == null) continue;

            foreach (var table in tables)
            {
                var rows = table.SelectNodes(".//tr");
                if (rows == null) continue;
                foreach (var row in rows)
                {
                    var cells = row.SelectNodes("th|td");
                    if (cells == null) continue;
                    var vals = new List<string>();
                    foreach (var c in cells)
                    {
                        var val = HtmlEntity.DeEntitize(c.InnerText);
                        if (val == null) continue;
                        vals.Add(val.Trim());
                    }
                    if (vals.Count < 2 || vals.Count > 3) continue;

                    var low = string.Join(" ", vals).ToLower();
                    if (low.StartsWith("позиция исключена") ||
                        low.StartsWith("(позиция введена") ||
                        low.StartsWith("(введено постановлением правительства") ||
                        low.StartsWith("наименование товара"))
                        continue;

                    string code = vals[1];
                    string name = vals[0];
                    string comment = vals.Count == 3 ? vals[2] : string.Empty;

                    var parts = CodePattern().Matches(code);
                    var codes = parts.Count > 1 ? string.Join(", ", parts.Select(m => m.Value)) : code;

                    foreach (var part in codes.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var codeClean = part.Trim();
                        string number = UrlNumberPattern().Match(url).Value;
                        var item = new AltaItem
                        {
                            Url = url,
                            Number = number,
                            Code = codeClean.Split('(')[0].Trim().Replace(")", "").Replace(" ", ""),
                            Name = name,
                            Comment = comment
                        };
                        items.Add(item);

                        if (low.Contains("за исключением"))
                        {
                            var excCode = ExceptionCodePrefix().Replace(code, "").Trim();
                            excCode = NonDigitComma().Replace(excCode, "");
                            exceptions.Add(new AltaException
                            {
                                Url = url,
                                Number = number,
                                Code = excCode.Replace(" ", ""),
                                Name = name,
                                Comment = comment
                            });
                        }
                    }
                }
            }
        }

        return (items, exceptions);
    }
}
