using HtmlAgilityPack;
using Quartz;

namespace Logibooks.Core.Services;

public class UpdateFeacnCodesJob(IHttpClientFactory httpClientFactory, ILogger<UpdateFeacnCodesJob> logger) : IJob
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<UpdateFeacnCodesJob> _logger = logger;

    private static readonly string[] FeacnOrders =
    [
        "https://www.alta.ru/tamdoc/10sr0318/"
        // TODO: add more urls
    ];

    private static readonly string[] IgnorePrefixes =
    [
        "позиция исключена",
        "(позиция введена",
        "(введено постановлением правительства",
        "наименование товара"
    ];

    public async Task Execute(IJobExecutionContext context)
    {
        var extracted = new List<FeacnCodeRow>();
        var token = context.CancellationToken;

        foreach (var url in FeacnOrders)
        {
            _logger.LogInformation("Downloading {Url}", url);
            string html;
            try
            {
                using var http = _httpClientFactory.CreateClient();
                html = await http.GetStringAsync(url, token);
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
                var rows = table.SelectNodes(".//tr");
                if (rows == null || rows.Count == 0) continue;

                var firstCells = rows[0].SelectNodes("./th|./td");
                int colCount = firstCells?.Count ?? 0;
                if (colCount < 2 || colCount > 3) continue;

                foreach (var row in rows)
                {
                    var cells = row.SelectNodes("./th|./td");
                    if (cells == null || cells.Count < 2 || cells.Count > 3) continue;

                    var cellTexts = cells.Select(c => HtmlEntity.DeEntitize(c.InnerText).Trim()).ToList();
                    if (ShouldIgnore(cellTexts)) continue;

                    string code;
                    string name;
                    string comment = string.Empty;

                    if (url == "https://www.alta.ru/tamdoc/10sr0318/")
                    {
                        if (cells.Count == 2)
                        {
                            code = cellTexts[1];
                            name = cellTexts[0];
                        }
                        else // 3 columns
                        {
                            code = cellTexts[1];
                            name = cellTexts[0];
                            comment = cellTexts[2];
                        }
                    }
                    else
                    {
                        code = cellTexts[0];
                        name = cellTexts.Count > 1 ? cellTexts[1] : string.Empty;
                        if (cellTexts.Count > 2) comment = cellTexts[2];
                    }

                    extracted.Add(new FeacnCodeRow(url, code, name, comment));
                }
            }
        }

        // TODO: further processing of extracted data
    }

    private static bool ShouldIgnore(IEnumerable<string> cells)
    {
        foreach (var cell in cells)
        {
            var text = cell.Trim().ToLowerInvariant();
            foreach (var prefix in IgnorePrefixes)
            {
                if (text.StartsWith(prefix)) return true;
            }
        }
        return false;
    }

    private record FeacnCodeRow(string Url, string Code, string Name, string Comment);
}

