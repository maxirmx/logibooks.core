// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Logibooks.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class ParcelFeacnCodeLookupServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pfcls_{System.Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Test]
    public async Task LookupAsync_AddsLinksAndRemovesExisting()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "This is SPAM" };
        ctx.Parcels.Add(order);
        var kw1 = new KeyWord { Id = 2, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        kw1.KeyWordFeacnCodes = new[] { new KeyWordFeacnCode { KeyWordId = 2, FeacnCode = "1", KeyWord = kw1 } };
        var kw2 = new KeyWord { Id = 3, Word = "other", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        kw2.KeyWordFeacnCodes = new[] { new KeyWordFeacnCode { KeyWordId = 3, FeacnCode = "2", KeyWord = kw2 } };
        ctx.KeyWords.AddRange(kw1, kw2);
        ctx.Set<BaseParcelKeyWord>().Add(new BaseParcelKeyWord { BaseParcelId = 1, KeyWordId = 99 });
        await ctx.SaveChangesAsync();

        var svc = new ParcelFeacnCodeLookupService(ctx, new MorphologySearchService());
        var wordsLookupContext = new WordsLookupContext<KeyWord>(ctx.KeyWords.ToList());
        var morphologyContext = new MorphologyContext();
        await svc.LookupAsync(order, morphologyContext, wordsLookupContext);

        var links = ctx.Set<BaseParcelKeyWord>().ToList();
        Assert.That(links.Count, Is.EqualTo(1));
        Assert.That(links.Single().KeyWordId, Is.EqualTo(2));
        Assert.That(ctx.Parcels.Find(1)!.CheckStatusId, Is.EqualTo(1));
    }

    [Test]
    public async Task LookupAsync_NoMatch_DoesNothing()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "clean" };
        ctx.Parcels.Add(order);
        var kw = new KeyWord { Id = 2, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        kw.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 2, FeacnCode = "1", KeyWord = kw }];
        ctx.KeyWords.Add(kw);
        await ctx.SaveChangesAsync();

        var svc = new ParcelFeacnCodeLookupService(ctx, new MorphologySearchService());
        var wordsLookupContext = new WordsLookupContext<KeyWord>(ctx.KeyWords.ToList());
        var morphologyContext = new MorphologyContext();
        await svc.LookupAsync(order, morphologyContext, wordsLookupContext);

        Assert.That(ctx.Set<BaseParcelKeyWord>().Any(), Is.False);
        Assert.That(ctx.Parcels.Find(1)!.CheckStatusId, Is.EqualTo(1));
    }

    [Test]
    public async Task LookupAsync_MixedKeywords_BothExactAndMorphology()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "This is SPAM with золотой браслет" };
        ctx.Parcels.Add(order);

        var kw1 = new KeyWord { Id = 10, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        kw1.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 10, FeacnCode = "1", KeyWord = kw1 }];
        var kw2 = new KeyWord { Id = 20, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        kw2.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 20, FeacnCode = "2", KeyWord = kw2 }];
        
        var keywords = new[] { kw1, kw2 };
        ctx.KeyWords.AddRange(keywords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var morphologyContext = morph.InitializeContext(keywords
            .Where(k => k.MatchTypeId >= (int)WordMatchTypeCode.MorphologyMatchTypes)
            .Select(k => new StopWord { Id = k.Id, Word = k.Word, MatchTypeId = k.MatchTypeId }));
        var wordsLookupContext = new WordsLookupContext<KeyWord>(keywords.Where(k => k.MatchTypeId == (int)WordMatchTypeCode.ExactSymbols));
        var svc = new ParcelFeacnCodeLookupService(ctx, morph);
        await svc.LookupAsync(order, morphologyContext, wordsLookupContext);

        var links = ctx.Set<BaseParcelKeyWord>().ToList();
        var foundIds = links.Select(l => l.KeyWordId).OrderBy(id => id).ToList();

        Assert.That(links.Count, Is.EqualTo(2));
        Assert.That(foundIds, Is.EquivalentTo(new[] { 10, 20 }));
    }

    [Test]
    public async Task LookupAsync_SkipsMarkedByPartner()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner,
            ProductName = "spam"
        };
        ctx.Parcels.Add(order);
        var kw = new KeyWord { Id = 2, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        kw.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 2, FeacnCode = "1", KeyWord = kw }];
        ctx.KeyWords.Add(kw);
        ctx.Set<BaseParcelKeyWord>().Add(new BaseParcelKeyWord { BaseParcelId = 1, KeyWordId = 99 });
        await ctx.SaveChangesAsync();

        var svc = new ParcelFeacnCodeLookupService(ctx, new MorphologySearchService());
        var wordsLookupContext = new WordsLookupContext<KeyWord>(ctx.KeyWords.ToList());
        var morphologyContext = new MorphologyContext();
        await svc.LookupAsync(order, morphologyContext, wordsLookupContext);

        var links = ctx.Set<BaseParcelKeyWord>().ToList();
        Assert.That(links.Count, Is.EqualTo(1));
        Assert.That(links.Single().KeyWordId, Is.EqualTo(99));
        Assert.That(ctx.Parcels.Find(1)!.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.MarkedByPartner));
    }
}
