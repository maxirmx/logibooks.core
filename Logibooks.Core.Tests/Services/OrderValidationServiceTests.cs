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

using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;

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

    [Test]
    public async Task ValidateAsync_AddsLinksAndUpdatesStatus()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "This is SPAM" };
        ctx.Orders.Add(order);
        ctx.StopWords.AddRange(
            new StopWord { Id = 2, Word = "spam", ExactMatch = true },
            new StopWord { Id = 3, Word = "other", ExactMatch = true }
        );
        ctx.Set<BaseOrderStopWord>().Add(new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 99 });
        await ctx.SaveChangesAsync();

        var svc = new OrderValidationService(ctx, new MorphologySearchService());
        await svc.ValidateAsync(order);

        Assert.That(ctx.Set<BaseOrderStopWord>().Count(), Is.EqualTo(1));
        var link = ctx.Set<BaseOrderStopWord>().Single();
        Assert.That(link.StopWordId, Is.EqualTo(2));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo(101));
    }

    [Test]
    public async Task ValidateAsync_NoMatch_DoesNothing()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "clean" };
        ctx.Orders.Add(order);
        ctx.StopWords.Add(new StopWord { Id = 2, Word = "spam", ExactMatch = true });
        await ctx.SaveChangesAsync();

        var svc = new OrderValidationService(ctx, new MorphologySearchService());
        await svc.ValidateAsync(order);

        Assert.That(ctx.Set<BaseOrderStopWord>().Any(), Is.False);
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo(201));
    }

    [Test]
    public async Task ValidateAsync_IgnoresCase()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "bad WORD" };
        ctx.Orders.Add(order);
        ctx.StopWords.Add(new StopWord { Id = 5, Word = "word", ExactMatch = true });
        await ctx.SaveChangesAsync();

        var svc = new OrderValidationService(ctx, new MorphologySearchService());
        await svc.ValidateAsync(order);

        Assert.That(ctx.Set<BaseOrderStopWord>().Single().StopWordId, Is.EqualTo(5));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo(101));
    }

    [Test]
    public async Task ValidateAsync_UsesMorphologyContext()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "золотой браслет" };
        ctx.Orders.Add(order);
        var sw = new StopWord { Id = 7, Word = "золото", ExactMatch = false };
        ctx.StopWords.Add(sw);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var context = morph.InitializeContext([sw]);
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var link = ctx.Set<BaseOrderStopWord>().Single();
        Assert.That(link.StopWordId, Is.EqualTo(7));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo(101));
    }

    // ========== MIXED STOPWORD SCENARIOS ==========

    [Test]
    public async Task ValidateAsync_MixedStopWords_BothExactAndMorphology()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "This is SPAM with золотой браслет" };
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
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        var foundIds = links.Select(l => l.StopWordId).OrderBy(id => id).ToList();
        
        Assert.That(links.Count, Is.EqualTo(2), "Should find exactly 2 matches");
        Assert.That(foundIds, Is.EquivalentTo([10, 20]), "Should find both exact and morphology matches");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_PreventsDuplicates()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "золотой браслет из золота" };
        ctx.Orders.Add(order);
        
        var stopWords = new[]
        {
            new StopWord { Id = 15, Word = "золото", ExactMatch = true },    // Should match via exact (exact word in text)
            new StopWord { Id = 25, Word = "золото", ExactMatch = false }    // Should match via morphology (derivative)
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var context = morph.InitializeContext(stopWords.Where(sw => !sw.ExactMatch));
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        var foundIds = links.Select(l => l.StopWordId).OrderBy(id => id).ToList();
        
        // Both stopwords have same root but different ExactMatch flags
        // Both should be found, no deduplication should occur as they have different IDs
        Assert.That(links.Count, Is.EqualTo(2), "Should find both stopword entries");
        Assert.That(foundIds, Is.EquivalentTo([15, 25]), "Should find both variants");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_OnlyMorphologyMatches()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "домашний уют и книжный магазин" };
        ctx.Orders.Add(order);
        
        var stopWords = new[]
        {
            new StopWord { Id = 40, Word = "spam", ExactMatch = true },      // Should NOT match
            new StopWord { Id = 50, Word = "дом", ExactMatch = false },      // Should match via morphology
            new StopWord { Id = 60, Word = "книга", ExactMatch = false },    // Should match via morphology
            new StopWord { Id = 70, Word = "metal", ExactMatch = true }      // Should NOT match
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var context = morph.InitializeContext(stopWords.Where(sw => !sw.ExactMatch));
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        var foundIds = links.Select(l => l.StopWordId).OrderBy(id => id).ToList();
        
        Assert.That(links.Count, Is.EqualTo(2), "Should find exactly 2 morphology matches");
        Assert.That(foundIds, Is.EquivalentTo([50, 60]), "Should find home and book derivatives");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_OnlyExactMatches()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "This contains SPAM and virus" };
        ctx.Orders.Add(order);
        
        var stopWords = new[]
        {
            new StopWord { Id = 80, Word = "spam", ExactMatch = true },      // Should match
            new StopWord { Id = 90, Word = "virus", ExactMatch = true },     // Should match
            new StopWord { Id = 100, Word = "дом", ExactMatch = false },     // Should NOT match
            new StopWord { Id = 110, Word = "книга", ExactMatch = false }    // Should NOT match
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var context = morph.InitializeContext(stopWords.Where(sw => !sw.ExactMatch));
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        var foundIds = links.Select(l => l.StopWordId).OrderBy(id => id).ToList();
        
        Assert.That(links.Count, Is.EqualTo(2), "Should find exactly 2 exact matches");
        Assert.That(foundIds, Is.EquivalentTo([80, 90]), "Should find spam and virus");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_NoMatches()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "clean product description" };
        ctx.Orders.Add(order);
        
        var stopWords = new[]
        {
            new StopWord { Id = 120, Word = "spam", ExactMatch = true },     // Should NOT match
            new StopWord { Id = 130, Word = "virus", ExactMatch = true },    // Should NOT match
            new StopWord { Id = 140, Word = "золото", ExactMatch = false },  // Should NOT match
            new StopWord { Id = 150, Word = "оружие", ExactMatch = false }   // Should NOT match
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var context = morph.InitializeContext(stopWords.Where(sw => !sw.ExactMatch));
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        
        Assert.That(links.Count, Is.EqualTo(0), "Should find no matches");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.NoIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_LargeSet()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "SPAM продукт с золотой отделкой и собачьего производства" };
        ctx.Orders.Add(order);
        
        var stopWords = new List<StopWord>();
        
        // Add many exact match words (only spam should match)
        for (int i = 200; i < 250; i++)
        {
            stopWords.Add(new StopWord { Id = i, Word = $"word{i}", ExactMatch = true });
        }
        stopWords.Add(new StopWord { Id = 250, Word = "spam", ExactMatch = true });      // Should match
        
        // Add many morphology words (only золото and собака should match)
        for (int i = 300; i < 350; i++)
        {
            stopWords.Add(new StopWord { Id = i, Word = $"слово{i}", ExactMatch = false });
        }
        stopWords.Add(new StopWord { Id = 350, Word = "золото", ExactMatch = false });   // Should match
        stopWords.Add(new StopWord { Id = 360, Word = "собака", ExactMatch = false });      // Should match
        
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var context = morph.InitializeContext(stopWords.Where(sw => !sw.ExactMatch));
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        var foundIds = links.Select(l => l.StopWordId).OrderBy(id => id).ToList();
        
        Assert.That(links.Count, Is.EqualTo(3), "Should find exactly 3 matches from large set");
        Assert.That(foundIds, Is.EquivalentTo([250, 350, 360]), "Should find spam, gold, and home matches");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_SameWordDifferentFlags()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "золотой дом из золота" };
        ctx.Orders.Add(order);
        
        var stopWords = new[]
        {
            // Same word with different ExactMatch flags - both should be found
            new StopWord { Id = 400, Word = "дом", ExactMatch = true },      // Should match exact word
            new StopWord { Id = 410, Word = "дом", ExactMatch = false },     // Should match via morphology
            new StopWord { Id = 420, Word = "золото", ExactMatch = true },   // Should match exact word
            new StopWord { Id = 430, Word = "золото", ExactMatch = false }   // Should match via morphology
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var context = morph.InitializeContext(stopWords.Where(sw => !sw.ExactMatch));
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        var foundIds = links.Select(l => l.StopWordId).OrderBy(id => id).ToList();
        
        Assert.That(links.Count, Is.EqualTo(4), "Should find all 4 stopword variants");
        Assert.That(foundIds, Is.EquivalentTo([400, 410, 420, 430]), "Should find all variants");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_CaseInsensitiveMatching()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "СПАМ продукт и КОШАЧЬЯ еда" };
        ctx.Orders.Add(order);
        
        var stopWords = new[]
        {
            new StopWord { Id = 500, Word = "спам", ExactMatch = true },     // Should match (case insensitive)
            new StopWord { Id = 510, Word = "КОШАЧЬЯ", ExactMatch = false }      // Should match via morphology (case insensitive)
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        var context = morph.InitializeContext(stopWords.Where(sw => !sw.ExactMatch));
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        var foundIds = links.Select(l => l.StopWordId).OrderBy(id => id).ToList();
        
        Assert.That(links.Count, Is.EqualTo(2), "Should find both case-insensitive matches");
        Assert.That(foundIds, Is.EquivalentTo([500, 510]), "Should handle case variations");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_EmptyMorphologyContext()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "SPAM product with домашний content" };
        ctx.Orders.Add(order);
        
        var stopWords = new[]
        {
            new StopWord { Id = 600, Word = "spam", ExactMatch = true },     // Should match
            new StopWord { Id = 610, Word = "дом", ExactMatch = false }      // Should NOT match (no context)
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var morph = new MorphologySearchService();
        // Pass empty context (no morphology words)
        var context = morph.InitializeContext(new StopWord[0]);
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        
        Assert.That(links.Count, Is.EqualTo(1), "Should find only exact match");
        Assert.That(links.Single().StopWordId, Is.EqualTo(600), "Should find only spam");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_MixedStopWords_NullMorphologyContext()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "SPAM product with домашний content" };
        ctx.Orders.Add(order);
        
        var stopWords = new[]
        {
            new StopWord { Id = 700, Word = "spam", ExactMatch = true },     // Should match
            new StopWord { Id = 710, Word = "дом", ExactMatch = false }      // Should NOT match (no context)
        };
        ctx.StopWords.AddRange(stopWords);
        await ctx.SaveChangesAsync();

        var svc = new OrderValidationService(ctx, new MorphologySearchService());
        // Pass null context
        await svc.ValidateAsync(order, null);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        
        Assert.That(links.Count, Is.EqualTo(1), "Should find only exact match");
        Assert.That(links.Single().StopWordId, Is.EqualTo(700), "Should find only spam");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task ValidateAsync_RemovesExistingLinksCorrectly()
    {
        using var ctx = CreateContext();
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, ProductName = "SPAM product" };
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

        var svc = new OrderValidationService(ctx, new MorphologySearchService());
        await svc.ValidateAsync(order);

        var links = ctx.Set<BaseOrderStopWord>().ToList();
        
        Assert.That(links.Count, Is.EqualTo(1), "Should replace existing links");
        Assert.That(links.Single().StopWordId, Is.EqualTo(800), "Should have new link only");
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo((int)OrderCheckStatusCode.HasIssues));
    }
}
