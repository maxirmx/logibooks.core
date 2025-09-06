// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using NUnit.Framework;

using Logibooks.Core.Models;

namespace Logibooks.Core.Tests.Models;

[TestFixture]
public class BaseOrderIndPostApiTests
{
    [Test]
    public void WbrOrder_IndPostApi_Methods_ReturnExpectedValues()
    {
        var order = new WbrParcel
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
            PassportNumber = "123467890",
            Category = "Категория",
            Subcategory = "Подкатегория"
        };

        Assert.That(order.GetParcelNumber(), Is.EqualTo("SHK123".PadLeft(20, '0')));
        Assert.That(order.GetCurrency(), Is.EqualTo("RUR"));
        Assert.That(order.GetDescription("Логибукс", "Тестовый заказ"), Does.Contain("УИН:"));
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
        Assert.That(order.GetSeries(), Is.EqualTo("1234"));
        Assert.That(order.GetNumber(), Is.EqualTo("67890"));
    }

    [Test]
    public void OzonOrder_IndPostApi_Methods_ReturnExpectedValues()
    {
        var order = new OzonParcel
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
            PassportSeries = "9876543210",
            PassportNumber = "9876543210"
        };

        Assert.That(order.GetParcelNumber(), Is.EqualTo("POST123"));
        Assert.That(order.GetCurrency(), Is.EqualTo("EUR"));
        Assert.That(order.GetDescription("Логибукс", "Тестовый заказ"), Does.Contain("УИН:"));
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
        Assert.That(order.GetSeries(), Is.EqualTo("98765"));
        Assert.That(order.GetNumber(), Is.EqualTo("43210"));
    }
}
