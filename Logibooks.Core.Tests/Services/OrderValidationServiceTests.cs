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
public class OrderValidationServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ov_{System.Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    // Add a helper method to create the service with mocked dependencies
    private static OrderValidationService CreateService(AppDbContext context)
    {
        var mockFeacnService = new Mock<IFeacnPrefixCheckService>();
        // Setup mock to return empty list by default
        mockFeacnService.Setup(x => x.CheckOrderAsync(It.IsAny<BaseOrder>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<BaseOrderFeacnPrefix>());

        return new OrderValidationService(context, new MorphologySearchService(), mockFeacnService.Object);
    }

    private static OrderValidationService CreateServiceWithMorphology(AppDbContext context, MorphologySearchService morphService)
    {
        var mockFeacnService = new Mock<IFeacnPrefixCheckService>();
        // Setup mock to return empty list by default
        mockFeacnService.Setup(x => x.CheckOrderAsync(It.IsAny<BaseOrder>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<BaseOrderFeacnPrefix>());

        return new OrderValidationService(context, morphService, mockFeacnService.Object);
    }

    [Test]
    public async Task ValidateAsync_AddsLinksAndUpdatesStatus()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "This is SPAM", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        ctx.StopWords.AddRange(
            new StopWord { Id = 2, Word = "spam", ExactMatch = true },
            new StopWord { Id = 3, Word = "other", ExactMatch = true }
        );
        ctx.Set<BaseOrderStopWord>().Add(new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 99 });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ValidateAsync(order);

        Assert.That(ctx.Set<BaseOrderStopWord>().Count(), Is.EqualTo(1));
        var link = ctx.Set<BaseOrderStopWord>().Single();
        Assert.That(link.StopWordId, Is.EqualTo(2));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_NoMatch_DoesNothing()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "clean", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        ctx.StopWords.Add(new StopWord { Id = 2, Word = "spam", ExactMatch = true });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ValidateAsync(order);

        Assert.That(ctx.Set<BaseOrderStopWord>().Any(), Is.False);
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.NoIssues));
    }

    [Test]
    public async Task ValidateAsync_IgnoresCase()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "bad WORD", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        ctx.StopWords.Add(new StopWord { Id = 5, Word = "word", ExactMatch = true });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ValidateAsync(order);

        Assert.That(ctx.Set<BaseOrderStopWord>().Single().StopWordId, Is.EqualTo(5));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_UsesMorphologyContext()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "золотой браслет", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        var sw = new StopWord { Id = 7, Word = "золото", ExactMatch = false };
        ctx.StopWords.Add(sw);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var context = morph.InitializeContext(new[] { sw });
        var svc = CreateServiceWithMorphology(ctx, morph);
        await svc.ValidateAsync(order, context);

        var link = ctx.Set<BaseOrderStopWord>().Single();
        Assert.That(link.StopWordId, Is.EqualTo(7));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_BothExactAndMorphology()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "This is SPAM with золотой браслет", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        
        var stopWords = new[]
        {
            new StopWord { Id = 10, Word = "spam", ExactMatch = true },      // Should match via exact
            new StopWord { Id = 20, Word = "золото", ExactMatch = false },   // Should match via morphology
            new StopWord { Id = 30, Word = "silver", ExactMatch = true }     // Should NOT match
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var context = morph.InitializeContext(stopWords.Where(sw => !sw.ExactMatch));
        var svc = CreateServiceWithMorphology(ctx, morph);
        await svc.ValidateAsync(order, context);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        var foundIds = links.Select(l => l.StopWordId).OrderBy(id => id).ToList();
        
        Assert.That(links.Count, Is.EqualTo(2), "Should find exactly 2 matches");
        Assert.That(foundIds, Is.EquivalentTo(new[] { 10, 20 }), "Should find both exact and morphology matches");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_RemovesExistingLinksCorrectly()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "SPAM product", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        
        // Add some existing links that should be removed
        ctx.Set<BaseOrderStopWord>().AddRange(
            new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 999 },
            new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 998 }
        );
        
        var stopWords = new[]
        {
            new StopWord { Id = 800, Word = "spam", ExactMatch = true }
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ValidateAsync(order);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        
        Assert.That(links.Count, Is.EqualTo(1), "Should replace existing links");
        Assert.That(links.Single().StopWordId, Is.EqualTo(800), "Should have new link only");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public void InitializeStopWordsContext_WithEmptyCollection_ReturnsEmptyContext()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var emptyStopWords = new StopWord[0];

        var context = svc.InitializeStopWordsContext(emptyStopWords);

        Assert.That(context, Is.Not.Null);
        Assert.That(context.ExactMatchStopWords, Is.Not.Null);
        Assert.That(context.ExactMatchStopWords, Is.Empty);
        Assert.That(context.ExactMatchStopWords.Count, Is.EqualTo(0));
    }

    [Test]
    public void InitializeStopWordsContext_WithOnlyExactMatchWords_AddsAllWords()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var stopWords = new[]
        {
            new StopWord { Id = 1, Word = "spam", ExactMatch = true },
            new StopWord { Id = 2, Word = "virus", ExactMatch = true },
            new StopWord { Id = 3, Word = "malware", ExactMatch = true }
        };

        var context = svc.InitializeStopWordsContext(stopWords);

        Assert.That(context, Is.Not.Null);
        Assert.That(context.ExactMatchStopWords.Count, Is.EqualTo(3));
        Assert.That(context.ExactMatchStopWords.Any(sw => sw.Word == "spam"), Is.True);
        Assert.That(context.ExactMatchStopWords.Any(sw => sw.Word == "virus"), Is.True);
        Assert.That(context.ExactMatchStopWords.Any(sw => sw.Word == "malware"), Is.True);
        Assert.That(context.ExactMatchStopWords.All(sw => sw.ExactMatch), Is.True);
    }

    [Test]
    public void InitializeStopWordsContext_WithMixedExactMatchFlags_OnlyAddsExactMatchWords()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var stopWords = new[]
        {
            new StopWord { Id = 1, Word = "spam", ExactMatch = true },       // Should be included
            new StopWord { Id = 2, Word = "золото", ExactMatch = false },    // Should NOT be included
            new StopWord { Id = 3, Word = "virus", ExactMatch = true },      // Should be included
            new StopWord { Id = 4, Word = "дом", ExactMatch = false },       // Should NOT be included
            new StopWord { Id = 5, Word = "malware", ExactMatch = true }     // Should be included
        };

        var context = svc.InitializeStopWordsContext(stopWords);

        Assert.That(context, Is.Not.Null);
        Assert.That(context.ExactMatchStopWords.Count, Is.EqualTo(3));
        
        var exactMatchWords = context.ExactMatchStopWords.Select(sw => sw.Word).ToList();
        Assert.That(exactMatchWords, Contains.Item("spam"));
        Assert.That(exactMatchWords, Contains.Item("virus"));
        Assert.That(exactMatchWords, Contains.Item("malware"));
        Assert.That(exactMatchWords, Does.Not.Contain("золото"));
        Assert.That(exactMatchWords, Does.Not.Contain("дом"));
        
        Assert.That(context.ExactMatchStopWords.All(sw => sw.ExactMatch), Is.True);
    }

    [Test]
    public async Task ValidateAsync_ProductNameIsEmpty_SetsNoIssuesAndNoLinks()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, ProductName = "", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        ctx.StopWords.AddRange(
            new StopWord { Id = 1, Word = "spam", ExactMatch = true },
            new StopWord { Id = 2, Word = "malware", ExactMatch = true }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ValidateAsync(order);

        Assert.That(ctx.Set<BaseOrderStopWord>().Any(), Is.False);
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.NoIssues));
    }

    [Test]
    public async Task ValidateAsync_WithStopWordsContext_UsesContextInsteadOfDatabase()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "This is SPAM", TnVed = "1234567890" };
        ctx.Orders.Add(order);
        
        // Add stop words to database (should be ignored when context is provided)
        ctx.StopWords.Add(new StopWord { Id = 999, Word = "spam", ExactMatch = true });
        await ctx.SaveChangesAsync();

        // Create context with different stop words
        var contextStopWords = new[]
        {
            new StopWord { Id = 100, Word = "spam", ExactMatch = true },
            new StopWord { Id = 200, Word = "virus", ExactMatch = true }
        };
        
        var svc = CreateService(ctx);
        var stopWordsContext = svc.InitializeStopWordsContext(contextStopWords);
        await svc.ValidateAsync(order, null, stopWordsContext);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        Assert.That(links.Count, Is.EqualTo(1));
        Assert.That(links.Single().StopWordId, Is.EqualTo(100), "Should use stop word from context, not database");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_ProductNameIsNull_SetsNoIssuesAndNoLinks()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, ProductName = null, TnVed = "1234567890" };
        ctx.Orders.Add(order);
        ctx.StopWords.Add(new StopWord { Id = 1, Word = "spam", ExactMatch = true });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ValidateAsync(order);

        Assert.That(ctx.Set<BaseOrderStopWord>().Any(), Is.False);
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.NoIssues));
    }
}
