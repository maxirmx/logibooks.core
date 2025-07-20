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
        await svc.ValidateAsync(order, null, stopWordsContext, null);

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


[Test]
    public async Task ValidateAsync_TnVedWithNonNumericChars_SetsInvalidFeacnFormatStatus()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "Test product", TnVed = "123abc7890" };
        ctx.Orders.Add(order);
        ctx.StopWords.Add(new StopWord { Id = 2, Word = "test", ExactMatch = true });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ValidateAsync(order);

        // Should not create any stop word links due to invalid TnVed
        Assert.That(ctx.Set<BaseOrderStopWord>().Any(), Is.False);
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.InvalidFeacnFormat));
    }

    [Test]
    public async Task ValidateAsync_TnVedTooShort_SetsInvalidFeacnFormatStatus()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "Test product", TnVed = "12345" };
        ctx.Orders.Add(order);
        ctx.StopWords.Add(new StopWord { Id = 2, Word = "test", ExactMatch = true });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ValidateAsync(order);

        // Should not create any stop word links due to invalid TnVed
        Assert.That(ctx.Set<BaseOrderStopWord>().Any(), Is.False);
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.InvalidFeacnFormat));
    }


    [Test]
    public async Task ValidateAsync_WithRealFeacnPrefixCheckService_CreatesFeacnAndStopWordLinks()
    {
        using var ctx = CreateContext();

        // Setup test data: FeacnOrder and FeacnPrefix
        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order", Comment = "Test order for validation" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1234",
            Description = "Restricted goods category",
            FeacnOrderId = 1
        };
        ctx.FeacnPrefixes.Add(feacnPrefix);

        // Add stop words that should match
        ctx.StopWords.AddRange(
            new StopWord { Id = 200, Word = "restricted", ExactMatch = true },
            new StopWord { Id = 201, Word = "banned", ExactMatch = true }
        );

        // Create order with TnVed that matches the FEACN prefix and product name with stop words
        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "This is a restricted item with banned substances",
            TnVed = "1234567890" // Matches prefix "1234"
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        // Use real FeacnPrefixCheckService instead of mock
        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        // Act
        await svc.ValidateAsync(order);

        // Assert - Check that both FEACN prefix links and stop word links were created
        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        var stopWordLinks = ctx.Set<BaseOrderStopWord>().Where(l => l.BaseOrderId == 1).ToList();

        // Should have one FEACN prefix link
        Assert.That(feacnLinks.Count, Is.EqualTo(1));
        Assert.That(feacnLinks.Single().FeacnPrefixId, Is.EqualTo(100));

        // Should have two stop word links
        Assert.That(stopWordLinks.Count, Is.EqualTo(2));
        var stopWordIds = stopWordLinks.Select(l => l.StopWordId).OrderBy(id => id).ToList();
        Assert.That(stopWordIds, Is.EquivalentTo(new[] { 200, 201 }));

        // Status should be HasIssues because both FEACN and stop word issues were found
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_WithRealFeacnPrefixCheckService_NoMatches_SetsNoIssues()
    {
        using var ctx = CreateContext();

        // Setup FEACN data that won't match
        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "9999", // Won't match TnVed "1234567890"
            FeacnOrderId = 1
        };
        ctx.FeacnPrefixes.Add(feacnPrefix);

        // Add stop words that won't match
        ctx.StopWords.Add(new StopWord { Id = 200, Word = "nonexistent", ExactMatch = true });

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Clean product name",
            TnVed = "1234567890"
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        // Act
        await svc.ValidateAsync(order);

        // Assert - No links should be created
        Assert.That(ctx.Set<BaseOrderFeacnPrefix>().Any(l => l.BaseOrderId == 1), Is.False);
        Assert.That(ctx.Set<BaseOrderStopWord>().Any(l => l.BaseOrderId == 1), Is.False);
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.NoIssues));
    }

    [Test]
    public async Task ValidateAsync_WithRealFeacnPrefixCheckService_FeacnExceptionPreventsMatch()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        // Create prefix that would match, but has an exception for the specific TnVed
        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1234",
            FeacnOrderId = 1
        };

        // Add exception that prevents match
        feacnPrefix.FeacnPrefixExceptions.Add(new FeacnPrefixException
        {
            Id = 150,
            Code = "123456", // This will prevent TnVed "1234567890" from matching
            FeacnPrefixId = 100
        });

        ctx.FeacnPrefixes.Add(feacnPrefix);

        // Add stop word that will match
        ctx.StopWords.Add(new StopWord { Id = 200, Word = "test", ExactMatch = true });

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "1234567890" // Starts with exception code "123456"
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        // Act
        await svc.ValidateAsync(order);

        // Assert - No FEACN link due to exception, but stop word link should exist
        Assert.That(ctx.Set<BaseOrderFeacnPrefix>().Any(l => l.BaseOrderId == 1), Is.False);
        Assert.That(ctx.Set<BaseOrderStopWord>().Count(l => l.BaseOrderId == 1), Is.EqualTo(1));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_WithRealFeacnPrefixCheckService_IntervalCodeMatch_CreatesFeacnLink()
    {
        using var ctx = CreateContext();

        // Setup FEACN data with interval matching
        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order with Interval" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1200000000",        // Left boundary: 1200000000
            IntervalCode = "1299999999", // Right boundary: 1299999999
            Description = "Interval-based restricted goods",
            FeacnOrderId = 1
        };
        ctx.FeacnPrefixes.Add(feacnPrefix);

        // Add stop word that will also match
        ctx.StopWords.Add(new StopWord { Id = 200, Word = "product", ExactMatch = true });

        // Create order with TnVed that falls within the interval range
        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product within interval range",
            TnVed = "1234567890" // This falls between 1200000000 and 1299999999
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        // Act
        await svc.ValidateAsync(order);

        // Assert - Check that both FEACN prefix link (interval match) and stop word link were created
        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        var stopWordLinks = ctx.Set<BaseOrderStopWord>().Where(l => l.BaseOrderId == 1).ToList();

        // Should have one FEACN prefix link from interval matching
        Assert.That(feacnLinks.Count, Is.EqualTo(1));
        Assert.That(feacnLinks.Single().FeacnPrefixId, Is.EqualTo(100));

        // Should have one stop word link
        Assert.That(stopWordLinks.Count, Is.EqualTo(1));
        Assert.That(stopWordLinks.Single().StopWordId, Is.EqualTo(200));

        // Status should be HasIssues because both FEACN interval and stop word issues were found
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }
    [Test]
    public async Task ValidateAsync_MatchesPrefix_PrefixMatchWithoutInterval_CreatesFeacnLink()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        // Create prefix without interval (Code only)
        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1234",
            IntervalCode = null, // No interval
            FeacnOrderId = 1
        };
        ctx.FeacnPrefixes.Add(feacnPrefix);

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "1234567890" // Starts with "1234"
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        await svc.ValidateAsync(order);

        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks.Count, Is.EqualTo(1));
        Assert.That(feacnLinks.Single().FeacnPrefixId, Is.EqualTo(100));
    }

    [Test]
    public async Task ValidateAsync_MatchesPrefix_PrefixDoesNotMatch_NoFeacnLink()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "9999",
            IntervalCode = null,
            FeacnOrderId = 1
        };
        ctx.FeacnPrefixes.Add(feacnPrefix);

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "1234567890" // Does not start with "9999"
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        await svc.ValidateAsync(order);

        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task ValidateAsync_MatchesPrefix_IntervalMatch_CreatesFeacnLink()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1000000000",       
            IntervalCode = "1099999999", 
            FeacnOrderId = 1
        };
        ctx.FeacnPrefixes.Add(feacnPrefix);

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "1050000000" // Falls within interval [1000000000, 1099999999]
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        await svc.ValidateAsync(order);

        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks.Count, Is.EqualTo(1));
        Assert.That(feacnLinks.Single().FeacnPrefixId, Is.EqualTo(100));
    }

    [Test]
    public async Task ValidateAsync_MatchesPrefix_IntervalBelowRange_NoFeacnLink()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "2000000000",        
            IntervalCode = "2099999999", 
            FeacnOrderId = 1
        };
        ctx.FeacnPrefixes.Add(feacnPrefix);

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "1500000000" 
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        await svc.ValidateAsync(order);

        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task ValidateAsync_MatchesPrefix_IntervalAboveRange_NoFeacnLink()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1000000000",        
            IntervalCode = "1099999999", 
            FeacnOrderId = 1
        };
        ctx.FeacnPrefixes.Add(feacnPrefix);

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "2500000000" 
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        await svc.ValidateAsync(order);

        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task ValidateAsync_MatchesPrefix_IntervalWithInvalidTnVed_NoFeacnLink()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1000000000",
            IntervalCode = "19099999999",
            FeacnOrderId = 1
        };
        ctx.FeacnPrefixes.Add(feacnPrefix);

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "10abc00000" // Invalid numeric format for interval comparison
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        await svc.ValidateAsync(order);

        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task ValidateAsync_MatchesPrefix_ExceptionPreventsMatch_NoFeacnLink()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1234", // Would match TnVed "1234567890"
            IntervalCode = null,
            FeacnOrderId = 1
        };

        // Add exception that prevents the match
        feacnPrefix.FeacnPrefixExceptions.Add(new FeacnPrefixException
        {
            Id = 150,
            Code = "123456", // TnVed starts with this exception code
            FeacnPrefixId = 100
        });

        ctx.FeacnPrefixes.Add(feacnPrefix);

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "1234567890" // Starts with exception code "123456"
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        await svc.ValidateAsync(order);

        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task ValidateAsync_MatchesPrefix_ExceptionDoesNotApply_CreatesFeacnLink()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1234",
            IntervalCode = null,
            FeacnOrderId = 1
        };

        // Add exception that does not apply to our TnVed
        feacnPrefix.FeacnPrefixExceptions.Add(new FeacnPrefixException
        {
            Id = 150,
            Code = "12349", // TnVed does not start with this
            FeacnPrefixId = 100
        });

        ctx.FeacnPrefixes.Add(feacnPrefix);

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "1234567890" // Does not start with exception code "12349"
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        await svc.ValidateAsync(order);

        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks.Count, Is.EqualTo(1));
        Assert.That(feacnLinks.Single().FeacnPrefixId, Is.EqualTo(100));
    }

    [Test]
    public async Task ValidateAsync_MatchesPrefix_EmptyExceptionCode_DoesNotPreventMatch()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1234",
            IntervalCode = null,
            FeacnOrderId = 1
        };

        // Add exception with empty code (should be ignored)
        feacnPrefix.FeacnPrefixExceptions.Add(new FeacnPrefixException
        {
            Id = 150,
            Code = "", // Empty exception code
            FeacnPrefixId = 100
        });

        ctx.FeacnPrefixes.Add(feacnPrefix);

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "1234567890"
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        await svc.ValidateAsync(order);

        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks.Count, Is.EqualTo(1));
        Assert.That(feacnLinks.Single().FeacnPrefixId, Is.EqualTo(100));
    }

    [Test]
    public async Task ValidateAsync_MatchesPrefix_IntervalBoundaryValues_CreatesFeacnLinks()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1000000000",        // LeftValue = 1000000000
            IntervalCode = "1099999999", // RightValue = 1999999999
            FeacnOrderId = 1
        };
        ctx.FeacnPrefixes.Add(feacnPrefix);

        // Test exact left boundary
        var order1 = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product 1",
            TnVed = "1000000000" // Exact left boundary
        };
        ctx.Orders.Add(order1);

        // Test exact right boundary
        var order2 = new WbrOrder
        {
            Id = 2,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product 2",
            TnVed = "1099999999" // Exact right boundary
        };
        ctx.Orders.Add(order2);

        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        // Test left boundary
        await svc.ValidateAsync(order1);
        var feacnLinks1 = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks1.Count, Is.EqualTo(1));

        // Test right boundary
        await svc.ValidateAsync(order2);
        var feacnLinks2 = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 2).ToList();
        Assert.That(feacnLinks2.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task ValidateAsync_MatchesPrefix_MultipleExceptions_FirstMatchingPreventsMatch()
    {
        using var ctx = CreateContext();

        var feacnOrder = new FeacnOrder { Id = 1, Title = "Test FEACN Order" };
        ctx.Add(feacnOrder);

        var feacnPrefix = new FeacnPrefix
        {
            Id = 100,
            Code = "1234",
            IntervalCode = null,
            FeacnOrderId = 1
        };

        // Add multiple exceptions
        foreach (var exception in new[]
        {
            new FeacnPrefixException { Id = 150, Code = "12349", FeacnPrefixId = 100 },
            new FeacnPrefixException { Id = 151, Code = "123456", FeacnPrefixId = 100 },
            new FeacnPrefixException { Id = 152, Code = "1234567", FeacnPrefixId = 100 }
        })
        {
            feacnPrefix.FeacnPrefixExceptions.Add(exception);
        }

        ctx.FeacnPrefixes.Add(feacnPrefix);

        var order = new WbrOrder
        {
            Id = 1,
            RegisterId = 1,
            CheckStatusId = 1,
            ProductName = "Test product",
            TnVed = "1234567890" // Starts with "123456" (second exception)
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var realFeacnService = new FeacnPrefixCheckService(ctx);
        var svc = new OrderValidationService(ctx, new MorphologySearchService(), realFeacnService);

        await svc.ValidateAsync(order);

        var feacnLinks = ctx.Set<BaseOrderFeacnPrefix>().Where(l => l.BaseOrderId == 1).ToList();
        Assert.That(feacnLinks.Count, Is.EqualTo(0));
    }
}
