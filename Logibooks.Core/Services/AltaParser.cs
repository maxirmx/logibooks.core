using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Logibooks.Core.Models;

namespace Logibooks.Core.Services;

public static class AltaParser
{
    private static readonly Regex CodePattern = new Regex(@"(\d{4}\s\d{2}\s\d{3}\s\d)", RegexOptions.Compiled);

    public static async Task<(List<AltaItem> Items, List<AltaException> Exceptions)> ParseAsync(IEnumerable<string> urls, HttpClient? client = null)
    {
        var http = client ?? new HttpClient();
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

                    string code = vals.Count == 2 ? vals[1] : vals[1];
                    string name = vals[0];
                    string comment = vals.Count == 3 ? vals[2] : string.Empty;

                    var parts = CodePattern.Matches(code);
                    var codes = parts.Count > 1 ? string.Join(", ", parts.Select(m => m.Value)) : code;

                    foreach (var part in codes.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var codeClean = part.Trim();
                        string number = Regex.Match(url, @"(\d{3})/?$").Value;
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
                            var excCode = Regex.Replace(code, @".*?\(", "").Trim();
                            excCode = Regex.Replace(excCode, @"[^\d,]+", "");
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
