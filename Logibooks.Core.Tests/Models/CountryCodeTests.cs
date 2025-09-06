// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System;
using NUnit.Framework;

using Logibooks.Core.Models;

namespace Logibooks.Core.Tests.Models;

public class CountryCodeTests
{
    [Test]
    public void LoadedAt_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;
        var cc = new Country { IsoNumeric = 123, IsoAlpha2 = "AA" };
        var after = DateTime.UtcNow;
        Assert.That(cc.LoadedAt, Is.GreaterThanOrEqualTo(before).And.LessThanOrEqualTo(after));
    }
}
