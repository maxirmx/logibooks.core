using System.Net;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Logibooks.Core.Services;
using Logibooks.Core.Data;
using Logibooks.Core.Models;

namespace Logibooks.Core.Tests.Services;

public class FakeHandler : HttpMessageHandler
{
    private readonly string _csv;
    public FakeHandler(string csv) { _csv = csv; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_csv)
        };
        return Task.FromResult(resp);
    }
}

[TestFixture]
public class UpdateCountryCodesServiceTests
{
    [Test]
    public async Task RunAsync_InsertsRecords()
    {
        var csv = "ISO3166-1-numeric,ISO3166-1-Alpha-2,UNTERM English Short,UNTERM English Formal,official_name_en,CLDR display name,UNTERM Russian Short,UNTERM Russian Formal,official_name_ru\n" +
                  "840,us,United States,United States of America,United States of America,United States,\u0421\u0428\u0410,\u0421\u043E\u0435\u0434\u0438\u043D\u0435\u043D\u043D\u044B\u0435 \u0428\u0442\u0430\u0442\u044B \u0410\u043C\u0435\u0440\u0438\u043A\u0438,\u0410\u043C\u0435\u0440\u0438\u043A\u0430";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cc_{Guid.NewGuid()}")
            .Options;
        using var ctx = new AppDbContext(options);
        var client = new HttpClient(new FakeHandler(csv));
        var svc = new UpdateCountryCodesService(ctx, NullLogger<UpdateCountryCodesService>.Instance, client);
        await svc.RunAsync();

        var cc = ctx.CountryCodes.Single();
        Assert.That(cc.IsoNumeric, Is.EqualTo(840));
        Assert.That(cc.IsoAlpha2, Is.EqualTo("US"));
        Assert.That(cc.NameEnShort, Is.EqualTo("United States"));
    }
}
