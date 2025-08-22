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

using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class ParcelValidationServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ov_{System.Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    // Add a helper method to create the service with mocked dependencies
    private static ParcelValidationService CreateService(AppDbContext context)
    {
        var mockFeacnService = new Mock<IFeacnPrefixCheckService>();
        // Setup mock to return empty list by default
        mockFeacnService.Setup(x => x.CheckOrderAsync(It.IsAny<BaseParcel>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<BaseOrderFeacnPrefix>());

        return new ParcelValidationService(context, new MorphologySearchService(), mockFeacnService.Object);
    }

    private static ParcelValidationService CreateServiceWithMorphology(AppDbContext context, MorphologySearchService morphService)
    {
        var mockFeacnService = new Mock<IFeacnPrefixCheckService>();
        // Setup mock to return empty list by default
        mockFeacnService.Setup(x => x.CheckOrderAsync(It.IsAny<BaseParcel>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<BaseOrderFeacnPrefix>());

        return new ParcelValidationService(context, morphService, mockFeacnService.Object);
    }

    [Test]
    public async Task ValidateAsync_AddsLinksAndUpdatesStatus()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "This is SPAM", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        ctx.StopWords.AddRange(
            new StopWord { Id = 2, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols },
            new StopWord { Id = 3, Word = "other", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }
        );
        ctx.Set<BaseOrderStopWord>().Add(new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 99 });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var wordsLookupContext = new WordsLookupContext<StopWord>(ctx.StopWords.ToList());
        var morphologyContext = new MorphologyContext(); // Assuming you have a way to create this context
        await svc.ValidateAsync(order, morphologyContext, wordsLookupContext);

        Assert.That(ctx.Set<BaseOrderStopWord>().Count(), Is.EqualTo(1));
        var link = ctx.Set<BaseOrderStopWord>().Single();
        Assert.That(link.StopWordId, Is.EqualTo(2));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_NoMatch_DoesNothing()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "clean", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        ctx.StopWords.Add(new StopWord { Id = 2, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var wordsLookupContext = new WordsLookupContext<StopWord>(ctx.StopWords.ToList());
        var morphologyContext = new MorphologyContext(); // Assuming you have a way to create this context
        await svc.ValidateAsync(order, morphologyContext, wordsLookupContext);

        Assert.That(ctx.Set<BaseOrderStopWord>().Any(), Is.False);
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.NoIssues));
    }

    [Test]
    public async Task ValidateAsync_IgnoresCase()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "bad WORD", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        ctx.StopWords.Add(new StopWord { Id = 5, Word = "word", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var wordsLookupContext = new WordsLookupContext<StopWord>(ctx.StopWords.ToList());
        var morphologyContext = new MorphologyContext(); // Assuming you have a way to create this context
        await svc.ValidateAsync(order, morphologyContext, wordsLookupContext);

        Assert.That(ctx.Set<BaseOrderStopWord>().Single().StopWordId, Is.EqualTo(5));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_UsesMorphologyContext()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "золотой браслет", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        var sw = new StopWord { Id = 7, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology };
        ctx.StopWords.Add(sw);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var morphologyContext = morph.InitializeContext(new[] { sw });
        var wordsLookupContext = new WordsLookupContext<StopWord>(Enumerable.Empty<StopWord>());
        var svc = CreateServiceWithMorphology(ctx, morph);
        await svc.ValidateAsync(order, morphologyContext, wordsLookupContext);

        var link = ctx.Set<BaseOrderStopWord>().Single();
        Assert.That(link.StopWordId, Is.EqualTo(7));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_BothExactAndMorphology()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "This is SPAM with золотой браслет", TnVed = "1234567890" };
        ctx.Orders.Add(order);

        var stopWords = new[]
        {
            new StopWord { Id = 10, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols },      // Should match via exact
            new StopWord { Id = 20, Word = "золото", MatchTypeId = (int)WordMatchTypeCode.StrongMorphology },   // Should match via morphology
            new StopWord { Id = 30, Word = "silver", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }     // Should NOT match
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var morphologyContext = morph.InitializeContext(stopWords.Where(sw => sw.MatchTypeId >= (int)WordMatchTypeCode.MorphologyMatchTypes));
        var svc = CreateServiceWithMorphology(ctx, morph);
        var wordsLookupContext = new WordsLookupContext<StopWord>(stopWords.Where(sw => sw.MatchTypeId == (int)WordMatchTypeCode.ExactSymbols));
        await svc.ValidateAsync(order, morphologyContext, wordsLookupContext);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        var foundIds = links.Select(l => l.StopWordId).OrderBy(id => id).ToList();

        Assert.That(links.Count, Is.EqualTo(2), "Should find exactly 2 matches");
        Assert.That(foundIds, Is.EquivalentTo(new[] { 10, 20 }), "Should find both exact and morphology matches");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_RemovesExistingLinksCorrectly()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "SPAM product", TnVed = "1234567890" };
        ctx.Orders.Add(order);

        // Add some existing links that should be removed
        ctx.Set<BaseOrderStopWord>().AddRange(
            new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 999 },
            new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 998 }
        );

        var stopWords = new[]
        {
            new StopWord { Id = 800, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var wordsLookupContext = new WordsLookupContext<StopWord>(stopWords);
        var morphologyContext = new MorphologyContext();
        await svc.ValidateAsync(order, morphologyContext, wordsLookupContext);

        var links = ctx.Set<BaseOrderStopWord>().ToList();

        Assert.That(links.Count, Is.EqualTo(1), "Should replace existing links");
        Assert.That(links.Single().StopWordId, Is.EqualTo(800), "Should have new link only");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_SkipsMarkedByPartner()
    {
        using var ctx = CreateContext();
        var order = new WbrParcel
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = (int)ParcelCheckStatusCode.MarkedByPartner,
            ProductName = "This is SPAM",
            TnVed = "1234567890"
        };
        ctx.Orders.Add(order);
        ctx.StopWords.Add(new StopWord { Id = 2, Word = "spam", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols });
        ctx.Set<BaseOrderStopWord>().Add(new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 99 });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var wordsLookupContext = new WordsLookupContext<StopWord>(ctx.StopWords.ToList());
        var morphologyContext = new MorphologyContext();
        await svc.ValidateAsync(order, morphologyContext, wordsLookupContext);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        Assert.That(links.Count, Is.EqualTo(1));
        Assert.That(links.Single().StopWordId, Is.EqualTo(99));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.MarkedByPartner));
    }

    [Test]
    public async Task ValidateAsync_NonexistingFeacn_SetsStatus()
    {
        using var ctx = CreateContext();
        ctx.FeacnCodes.Add(new FeacnCode
        {
            Id = 1,
            Code = "1111111111",
            CodeEx = "1111111111",
            Name = "name",
            NormalizedName = "name"
        });
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var wordsLookupContext = new WordsLookupContext<StopWord>(Enumerable.Empty<StopWord>());
        var morphologyContext = new MorphologyContext();
        await svc.ValidateAsync(order, morphologyContext, wordsLookupContext);

        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.NonexistingFeacn));
    }

    [Test]
    public async Task ValidateAsync_ExistingFeacn_ContinuesProcessing()
    {
        using var ctx = CreateContext();
        ctx.FeacnCodes.Add(new FeacnCode
        {
            Id = 1,
            Code = "1234567890",
            CodeEx = "1234567890",
            Name = "name",
            NormalizedName = "name"
        });
        var order = new WbrParcel { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var wordsLookupContext = new WordsLookupContext<StopWord>(Enumerable.Empty<StopWord>());
        var morphologyContext = new MorphologyContext();
        await svc.ValidateAsync(order, morphologyContext, wordsLookupContext);

        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)ParcelCheckStatusCode.NoIssues));
    }

    [Test]
    public void GetMatchingWords_IgnoresEmptyStopWord()
    {
        // Arrange
        var emptyStopWord = new StopWord { Id = 1, Word = string.Empty, MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        var context = new WordsLookupContext<StopWord>(new[] { emptyStopWord });
        var productName = "Some product name";

        // Act
        var result = context.GetMatchingWords(productName).ToList();

        // Assert
        Assert.That(result.Count, Is.EqualTo(0), "Should not match empty stopword");
    }
}
