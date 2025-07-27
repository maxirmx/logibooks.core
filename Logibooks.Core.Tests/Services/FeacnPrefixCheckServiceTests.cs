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
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class FeacnPrefixCheckServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"fp_{System.Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private FeacnOrder _enabledOrder;
    private AppDbContext _context;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [SetUp]
    public void Setup()
    {
        _context = CreateContext();
        _enabledOrder = new FeacnOrder { Id = 1, Title = "Enabled", Enabled = true };
        _context.FeacnOrders.Add(_enabledOrder);
        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task CheckOrderAsync_MatchesPrefix_AddsLink()
    {
        var prefix = new FeacnPrefix { Id = 10, Code = "1234", FeacnOrderId = 1 };
        _context.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(1));
        var link = links.First();
        Assert.That(link.BaseOrderId, Is.EqualTo(1));
        Assert.That(link.FeacnPrefixId, Is.EqualTo(10));

    }

    [Test]
    public async Task CheckOrderAsync_NoMatch_SetsStatusNoIssuesAndReturnsEmptyList()
    {
        _context.FeacnPrefixes.Add(new FeacnPrefix { Id = 10, Code = "9999", FeacnOrderId = 1 });
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(0));
        Assert.That(_context.Set<BaseOrderFeacnPrefix>().Any(), Is.False);
    }

    [Test]
    public async Task CheckOrderAsync_ExceptionPreventsMatch_ReturnsEmptyList()
    {
        var prefix = new FeacnPrefix { Id = 10, Code = "1234", IntervalCode = "56", FeacnOrderId = 1 };
        prefix.FeacnPrefixExceptions.Add(new FeacnPrefixException { Id = 20, Code = "123455" });
        _context.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234550000" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(0));
        Assert.That(_context.Set<BaseOrderFeacnPrefix>().Any(), Is.False);
    }

    [Test]
    public async Task CheckOrderAsync_MultipleMatches_ReturnsAllLinks()
    {
        var prefix1 = new FeacnPrefix { Id = 10, Code = "12", FeacnOrderId = 1 };
        var prefix2 = new FeacnPrefix { Id = 11, Code = "1234", FeacnOrderId = 1 };
        _context.FeacnPrefixes.AddRange(prefix1, prefix2);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(2));
        Assert.That(links.Select(l => l.FeacnPrefixId), Is.EquivalentTo(new[] { 10, 11 }));
    }

    [Test]
    public async Task CheckOrderAsync_IntervalMatch_ReturnsLink()
    {
        var prefix = new FeacnPrefix { Id = 10, Code = "1200000000", IntervalCode = "1299999999", FeacnOrderId = 1 };
        _context.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(1));
        var link = links.First();
        Assert.That(link.FeacnPrefixId, Is.EqualTo(10));
    }

    [Test]
    public async Task CheckOrderAsync_IntervalNoMatch_ReturnsEmptyList()
    {
        var prefix = new FeacnPrefix { Id = 10, Code = "1300000000", IntervalCode = "1399999999", FeacnOrderId = 1 };
        _context.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task CheckOrderAsync_TnVedTooShort_ReturnsEmptyList()
    {
        var prefix = new FeacnPrefix { Id = 10, Code = "1234", FeacnOrderId = 1 };
        _context.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task CheckOrderAsync_PrefixTooShort_ReturnsEmptyList()
    {
        var prefix = new FeacnPrefix { Id = 10, Code = "1", FeacnOrderId = 1 };
        _context.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task CheckOrderAsync_TnVedNotStartingWithPrefix_ReturnsEmptyList()
    {
        var prefix = new FeacnPrefix { Id = 10, Code = "99", FeacnOrderId = 1 };
        _context.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task CheckOrderAsync_InvalidTnVedForInterval_ReturnsEmptyList()
    {
        var prefix = new FeacnPrefix { Id = 10, Code = "1200000000", IntervalCode = "1299999999", FeacnOrderId = 1 };
        _context.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "abcd567890" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task CheckOrderAsync_EmptyExceptionCode_DoesNotPreventMatch()
    {
        var prefix = new FeacnPrefix { Id = 10, Code = "1234", FeacnOrderId = 1 };
        // Use valid non-empty codes for the exceptions since Code is required
        prefix.FeacnPrefixExceptions.Add(new FeacnPrefixException { Id = 20, Code = "9999" }); // Different code that won't match
        prefix.FeacnPrefixExceptions.Add(new FeacnPrefixException { Id = 21, Code = "8888" }); // Different code that won't match
        _context.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task CheckOrderAsync_NoPrefixesForTwoDigitPrefix_ReturnsEmptyList()
    {
        var prefix = new FeacnPrefix { Id = 10, Code = "9999", FeacnOrderId = 1 };
        _context.FeacnPrefixes.Add(prefix);
        var order = new WbrOrder { Id = 1, RegisterId = 1, CheckStatusId = 1, TnVed = "1234567890" };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var links = await svc.CheckOrderAsync(order);

        Assert.That(order.CheckStatusId, Is.EqualTo(1));
        Assert.That(links.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task CreateContext_GroupsPrefixesByTwoDigits()
    {
        var enabledOrder = new FeacnOrder { Id = 2, Title = "Enabled", Enabled = true };
        _context.FeacnOrders.Add(enabledOrder);
        var p1 = new FeacnPrefix { Id = 1, Code = "1200", FeacnOrderId = 1, FeacnOrder = enabledOrder };
        var p2 = new FeacnPrefix { Id = 2, Code = "1299", FeacnOrderId = 1, FeacnOrder = enabledOrder };
        var p3 = new FeacnPrefix { Id = 3, Code = "9900", FeacnOrderId = 1, FeacnOrder = enabledOrder };
        _context.FeacnPrefixes.AddRange(p1, p2, p3);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var context = await svc.CreateContext();

        Assert.That(context.Prefixes.ContainsKey("12"));
        Assert.That(context.Prefixes.ContainsKey("99"));
        Assert.That(context.Prefixes["12"].Count, Is.EqualTo(2));
        Assert.That(context.Prefixes["99"].Count, Is.EqualTo(1));
    }

    [Test]
    public async Task CreateContext_IgnoresShortCodes()
    {
        var shortPrefix = new FeacnPrefix { Id = 5, Code = "1", FeacnOrderId = 1 };
        _context.FeacnPrefixes.Add(shortPrefix);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var context = await svc.CreateContext();

        var allIds = context.Prefixes.SelectMany(kvp => kvp.Value).Select(p => p.Id);
        Assert.That(allIds, Does.Not.Contain(5));
    }

    [Test]
    public async Task CreateContext_LoadsExceptions()
    {
        var enabledOrder = new FeacnOrder { Id = 2, Title = "Enabled", Enabled = true };
        _context.FeacnOrders.Add(enabledOrder);
        var prefix = new FeacnPrefix { Id = 10, Code = "1234", FeacnOrderId = 1, FeacnOrder = enabledOrder };
        prefix.FeacnPrefixExceptions.Add(new FeacnPrefixException { Id = 20, Code = "123455" });
        _context.FeacnPrefixes.Add(prefix);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var context = await svc.CreateContext();

        var loaded = context.Prefixes["12"].First(p => p.Id == 10);
        Assert.That(loaded.FeacnPrefixExceptions.Count, Is.EqualTo(1));
        Assert.That(loaded.FeacnPrefixExceptions.First().Code, Is.EqualTo("123455"));
    }

    [Test]
    public async Task CreateContext_SkipsPrefixesWithDisabledFeacnOrder()
    {
        var enabledOrder = new FeacnOrder { Id = 2, Title = "Enabled", Enabled = true };
        var disabledOrder = new FeacnOrder { Id = 3, Title = "Disabled", Enabled = false };
        _context.FeacnOrders.AddRange(enabledOrder, disabledOrder);
        var p1 = new FeacnPrefix { Id = 1, Code = "1200", FeacnOrderId = 1, FeacnOrder = enabledOrder };
        var p2 = new FeacnPrefix { Id = 2, Code = "1299", FeacnOrderId = 2, FeacnOrder = disabledOrder };
        _context.FeacnPrefixes.AddRange(p1, p2);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var context = await svc.CreateContext();

        var allIds = context.Prefixes.SelectMany(kvp => kvp.Value).Select(p => p.Id).ToList();
        Assert.That(allIds, Does.Contain(1));
        Assert.That(allIds, Does.Not.Contain(2));
    }

    [Test]
    public async Task CreateContext_IncludesPrefixesWithEnabledFeacnOrder()
    {
        var enabledOrder = new FeacnOrder { Id = 2, Title = "Enabled", Enabled = true };
        _context.FeacnOrders.Add(enabledOrder);
        var p1 = new FeacnPrefix { Id = 1, Code = "1200", FeacnOrderId = 1, FeacnOrder = enabledOrder };
        _context.FeacnPrefixes.Add(p1);
        await _context.SaveChangesAsync();

        var svc = new FeacnPrefixCheckService(_context);
        var context = await svc.CreateContext();

        var allIds = context.Prefixes.SelectMany(kvp => kvp.Value).Select(p => p.Id).ToList();
        Assert.That(allIds, Does.Contain(1));
    }

}
