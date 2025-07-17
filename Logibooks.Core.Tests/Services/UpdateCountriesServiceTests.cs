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

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

using Logibooks.Core.Data;
using Logibooks.Core.Services;
using Logibooks.Core.Models;

namespace Logibooks.Core.Tests.Services;

public class FakeCountryCodesHandler : HttpMessageHandler
{
    private readonly string _csv;
    public FakeCountryCodesHandler(string csv) { _csv = csv; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_csv)
        };
        return Task.FromResult(resp);
    }
}

public class FakeErrorHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _code;
    public FakeErrorHandler(HttpStatusCode code) { _code = code; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_code));
    }
}

[TestFixture]
public class UpdateCountriesServiceTests
{
    private static IHttpClientFactory CreateHttpClientFactory(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("", options => { }).ConfigurePrimaryHttpMessageHandler(() => handler);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IHttpClientFactory>();
    }

    private static IHttpClientFactory CreateHttpClientFactory(string csv)
        => CreateHttpClientFactory(new FakeCountryCodesHandler(csv));

    [Test]
    public async Task RunAsync_InsertsRecords()
    {
        var csv = "ISO3166-1-numeric,ISO3166-1-Alpha-2,UNTERM English Short,UNTERM English Formal,official_name_en,CLDR display name,UNTERM Russian Short,UNTERM Russian Formal,official_name_ru\n" +
                  "840,us,United States,United States of America,United States of America,United States,ÑØÀ,Ñîåäèí¸ííûå Øòàòû Àìåðèêè,Àìåðèêà";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cc_{Guid.NewGuid()}")
            .Options;
        using var ctx = new AppDbContext(options);
        var httpClientFactory = CreateHttpClientFactory(csv);
        var svc = new UpdateCountriesService(ctx, NullLogger<UpdateCountriesService>.Instance, httpClientFactory);
        await svc.RunAsync();

        var cc = ctx.Countries.Single();
        Assert.That(cc.IsoNumeric, Is.EqualTo(840));
        Assert.That(cc.IsoAlpha2, Is.EqualTo("US"));
        Assert.That(cc.NameEnShort, Is.EqualTo("United States"));
    }

    [Test]
    public async Task RunAsync_UpdatesExistingRecord()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cc_{Guid.NewGuid()}")
            .Options;
        using var ctx = new AppDbContext(options);
        ctx.Countries.Add(new Country
        {
            IsoNumeric = 840,
            IsoAlpha2 = "XX",
            NameEnShort = "Old Name",
            NameEnFormal = "",
            NameEnOfficial = "",
            NameEnCldr = "",
            NameRuShort = "",
            NameRuFormal = "",
            NameRuOfficial = "",
            LoadedAt = DateTime.MinValue
        });
        ctx.SaveChanges();

        var csv = "ISO3166-1-numeric,ISO3166-1-Alpha-2,UNTERM English Short,UNTERM English Formal,official_name_en,CLDR display name,UNTERM Russian Short,UNTERM Russian Formal,official_name_ru\n" +
                  "840,us,United States,United States of America,United States of America,United States,ÑØÀ,Ñîåäèí¸ííûå Øòàòû Àìåðèêè,Àìåðèêà";
        var httpClientFactory = CreateHttpClientFactory(csv);
        var svc = new UpdateCountriesService(ctx, NullLogger<UpdateCountriesService>.Instance, httpClientFactory);
        await svc.RunAsync();

        var cc = ctx.Countries.Single();
        Assert.That(cc.IsoAlpha2, Is.EqualTo("US"));
        Assert.That(cc.NameEnShort, Is.EqualTo("United States"));
        Assert.That(cc.LoadedAt, Is.Not.EqualTo(DateTime.MinValue));
    }

    [Test]
    public async Task RunAsync_AddsAndUpdates_Mixed()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cc_{Guid.NewGuid()}")
            .Options;
        using var ctx = new AppDbContext(options);
        ctx.Countries.Add(new Country
        {
            IsoNumeric = 840,
            IsoAlpha2 = "XX",
            NameEnShort = "Old Name",
            NameEnFormal = "",
            NameEnOfficial = "",
            NameEnCldr = "",
            NameRuShort = "",
            NameRuFormal = "",
            NameRuOfficial = "",
            LoadedAt = DateTime.MinValue
        });
        ctx.SaveChanges();

        var csv = "ISO3166-1-numeric,ISO3166-1-Alpha-2,UNTERM English Short,UNTERM English Formal,official_name_en,CLDR display name,UNTERM Russian Short,UNTERM Russian Formal,official_name_ru\n" +
                  "840,us,United States,United States of America,United States of America,United States,ÑØÀ,Ñîåäèí¸ííûå Øòàòû Àìåðèêè,Àìåðèêà\n" +
                  "124,ca,Canada,Canada,Canada,Canada,Êàíàäà,Êàíàäà,Êàíàäà";
        var httpClientFactory = CreateHttpClientFactory(csv);
        var svc = new UpdateCountriesService(ctx, NullLogger<UpdateCountriesService>.Instance, httpClientFactory);
        await svc.RunAsync();

        Assert.That(ctx.Countries.Count(), Is.EqualTo(2));
        Assert.That(ctx.Countries.Any(c => c.IsoNumeric == 124 && c.IsoAlpha2 == "CA"));
        Assert.That(ctx.Countries.Any(c => c.IsoNumeric == 840 && c.IsoAlpha2 == "US"));
    }

    [Test]
    public async Task RunAsync_UppercasesIsoAlpha2()
    {
        var csv = "ISO3166-1-numeric,ISO3166-1-Alpha-2,UNTERM English Short,UNTERM English Formal,official_name_en,CLDR display name,UNTERM Russian Short,UNTERM Russian Formal,official_name_ru\n" +
                  "840,Us,United States,United States of America,United States of America,United States,ÑØÀ,Ñîåäèí¸ííûå Øòàòû Àìåðèêè,Àìåðèêà";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cc_{Guid.NewGuid()}")
            .Options;
        using var ctx = new AppDbContext(options);
        var httpClientFactory = CreateHttpClientFactory(csv);
        var svc = new UpdateCountriesService(ctx, NullLogger<UpdateCountriesService>.Instance, httpClientFactory);
        await svc.RunAsync();

        var cc = ctx.Countries.Single();
        Assert.That(cc.IsoAlpha2, Is.EqualTo("US"));
    }

    [Test]
    public async Task RunAsync_HandlesEmptyCsv()
    {
        var csv = "ISO3166-1-numeric,ISO3166-1-Alpha-2,UNTERM English Short,UNTERM English Formal,official_name_en,CLDR display name,UNTERM Russian Short,UNTERM Russian Formal,official_name_ru\n";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cc_{Guid.NewGuid()}")
            .Options;
        using var ctx = new AppDbContext(options);
        var httpClientFactory = CreateHttpClientFactory(csv);
        var svc = new UpdateCountriesService(ctx, NullLogger<UpdateCountriesService>.Instance, httpClientFactory);
        await svc.RunAsync();

        Assert.That(ctx.Countries.Count(), Is.EqualTo(0));
    }

    [Test]
    public void RunAsync_RespectsCancellationToken()
    {
        var csv = "ISO3166-1-numeric,ISO3166-1-Alpha-2,UNTERM English Short,UNTERM English Formal,official_name_en,CLDR display name,UNTERM Russian Short,UNTERM Russian Formal,official_name_ru\n" +
                  "840,us,United States,United States of America,United States of America,United States,ÑØÀ,Ñîåäèí¸ííûå Øòàòû Àìåðèêè,Àìåðèêà";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cc_{Guid.NewGuid()}")
            .Options;
        using var ctx = new AppDbContext(options);
        var httpClientFactory = CreateHttpClientFactory(csv);
        var svc = new UpdateCountriesService(ctx, NullLogger<UpdateCountriesService>.Instance, httpClientFactory);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(
            async () => await svc.RunAsync(cts.Token),
            Throws.InstanceOf<OperationCanceledException>()
        );
    }

    [Test]
    public void RunAsync_ThrowsOnHttpError()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cc_{Guid.NewGuid()}")
            .Options;
        using var ctx = new AppDbContext(options);
        var httpClientFactory = CreateHttpClientFactory(new FakeErrorHandler(HttpStatusCode.InternalServerError));
        var svc = new UpdateCountriesService(ctx, NullLogger<UpdateCountriesService>.Instance, httpClientFactory);

        Assert.That(async () => await svc.RunAsync(), Throws.InstanceOf<HttpRequestException>());
    }
}
