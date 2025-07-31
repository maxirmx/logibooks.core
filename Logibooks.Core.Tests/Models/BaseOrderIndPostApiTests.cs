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

using NUnit.Framework;

using Logibooks.Core.Models;

namespace Logibooks.Core.Tests.Models;

[TestFixture]
public class BaseOrderIndPostApiTests
{
    [Test]
    public void WbrOrder_IndPostApi_Methods_ReturnExpectedValues()
    {
        var order = new WbrOrder
        {
            Id = 1,
            Shk = "SHK123",
            Currency = "RUR",
            ProductName = "Test Product",
            Quantity = 2,
            UnitPrice = 10.5m,
            WeightKg = 1.234m,
            ProductLink = "https://example.com/product",
            RecipientAddress = "Россия, Москва, ул. Тестовая, д.1",
            RecipientName = "Иванов Иван Иванович",
            PassportNumber = "1234567890",
            Category = "Категория",
            Subcategory = "Подкатегория"
        };

        Assert.That(order.GetParcelNumber(), Is.EqualTo("SHK123".PadLeft(20, '0')));
        Assert.That(order.GetCurrency(), Is.EqualTo("RUR"));
        Assert.That(order.GetDescription(), Does.Contain("УИН:"));
        Assert.That(order.GetQuantity(), Is.EqualTo("2"));
        Assert.That(order.GetCost(), Is.EqualTo("21.00"));
        Assert.That(order.GetWeight(), Is.EqualTo("1.234"));
        Assert.That(order.GetUrl(), Is.EqualTo("https://example.com/product"));
        Assert.That(order.GetCity(), Is.EqualTo("Москва"));
        Assert.That(order.GetStreet(), Does.Contain("ул. Тестовая"));
        Assert.That(order.GetSurName(), Is.EqualTo("Иванов"));
        Assert.That(order.GetName(), Is.EqualTo("Иван"));
        Assert.That(order.GetMiddleName(), Is.EqualTo("Иванович"));
        Assert.That(order.GetFullName(), Is.EqualTo("Иванов Иван Иванович"));
        // Series and Number are not set in WbrOrder, expect placeholders
        Assert.That(order.GetSeries(), Is.EqualTo("12345"));
        Assert.That(order.GetNumber(), Is.EqualTo("67890"));
    }

    [Test]
    public void OzonOrder_IndPostApi_Methods_ReturnExpectedValues()
    {
        var order = new OzonOrder
        {
            Id = 2,
            PostingNumber = "POST123",
            Currency = "EUR",
            ProductName = "Ozon Product",
            Article = "ART987",
            Quantity = 3,
            UnitPrice = 15.5m,
            WeightKg = 2.345m,
            ProductLink = "https://ozon.ru/product/ozon-product",
            City = "Санкт-Петербург",
            Address = "ул. Озон, д.2",
            LastName = "Петров",
            FirstName = "Петр",
            Patronymic = "Петрович",
            PassportSeries = "AB",
            PassportNumber = "9876543210"
        };

        Assert.That(order.GetParcelNumber(), Is.EqualTo("POST123"));
        Assert.That(order.GetCurrency(), Is.EqualTo("EUR"));
        Assert.That(order.GetDescription(), Does.Contain("УИН:"));
        Assert.That(order.GetQuantity(), Is.EqualTo("3"));
        Assert.That(order.GetCost(), Is.EqualTo("46.50"));
        Assert.That(order.GetWeight(), Is.EqualTo("2.345"));
        Assert.That(order.GetUrl(), Is.EqualTo("https://ozon.ru/product/ozon-product"));
        Assert.That(order.GetCity(), Is.EqualTo("Санкт-Петербург"));
        Assert.That(order.GetStreet(), Is.EqualTo("ул. Озон, д.2"));
        Assert.That(order.GetSurName(), Is.EqualTo("Петров"));
        Assert.That(order.GetName(), Is.EqualTo("Петр"));
        Assert.That(order.GetMiddleName(), Is.EqualTo("Петрович"));
        Assert.That(order.GetFullName(), Is.EqualTo("Петров Петр Петрович"));
        Assert.That(order.GetSeries(), Is.EqualTo("AB"));
        Assert.That(order.GetNumber(), Is.EqualTo("9876543210"));
    }
}
