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
        var context = morph.InitializeContext(new[] { sw });
        var svc = new OrderValidationService(ctx, morph);
        await svc.ValidateAsync(order, context);

        var link = ctx.Set<BaseOrderStopWord>().Single();
        Assert.That(link.StopWordId, Is.EqualTo(7));
        Assert.That(ctx.Orders.Find(1)!.CheckStatusId, Is.EqualTo(101));
    }
}
