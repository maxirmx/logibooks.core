// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Threading;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class RegisterProcessingServiceCountryLookupTests
{
    private AppDbContext _context = null!;
    private RegisterProcessingService _service = null!;
    private ILogger<RegisterProcessingService> _logger = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _logger = new LoggerFactory().CreateLogger<RegisterProcessingService>();
        _service = new RegisterProcessingService(_context, _logger);

        // Add test countries
        _context.Countries.AddRange([
            new Country 
            { 
                IsoNumeric = 643, 
                IsoAlpha2 = "RU", 
                NameRuShort = "Российская Федерация",
                NameEnShort = "Russian Federation",
                NameEnFormal = "Russian Federation",
                NameEnOfficial = "Russian Federation", 
                NameEnCldr = "Russia",
                NameRuFormal = "Российская Федерация",
                NameRuOfficial = "Российская Федерация"
            },
            new Country 
            { 
                IsoNumeric = 860, 
                IsoAlpha2 = "UZ", 
                NameRuShort = "Узбекистан",
                NameEnShort = "Uzbekistan",
                NameEnFormal = "Republic of Uzbekistan",
                NameEnOfficial = "Republic of Uzbekistan",
                NameEnCldr = "Uzbekistan",
                NameRuFormal = "Республика Узбекистан",
                NameRuOfficial = "Республика Узбекистан"
            },
            new Country 
            { 
                IsoNumeric = 840, 
                IsoAlpha2 = "US", 
                NameRuShort = "США",
                NameEnShort = "United States",
                NameEnFormal = "United States of America",
                NameEnOfficial = "United States of America",
                NameEnCldr = "United States",
                NameRuFormal = "Соединенные Штаты Америки",
                NameRuOfficial = "Соединенные Штаты Америки"
            },
            new Country 
            { 
                IsoNumeric = 156, 
                IsoAlpha2 = "CN", 
                NameRuShort = "Китай",
                NameEnShort = "China",
                NameEnFormal = "People's Republic of China",
                NameEnOfficial = "People's Republic of China",
                NameEnCldr = "China",
                NameRuFormal = "Китайская Народная Республика",
                NameRuOfficial = "Китайская Народная Республика"
            },
            new Country 
            { 
                IsoNumeric = 276, 
                IsoAlpha2 = "DE", 
                NameRuShort = "Германия",
                NameEnShort = "Germany",
                NameEnFormal = "Federal Republic of Germany",
                NameEnOfficial = "Federal Republic of Germany",
                NameEnCldr = "Germany",
                NameRuFormal = "Федеративная Республика Германия",
                NameRuOfficial = "Федеративная Республика Германия"
            }
        ]);
        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task LookupCountryCode_ByIsoAlpha2_ReturnsCorrectCode()
    {
        // Use reflection to access private method for testing
        var method = typeof(RegisterProcessingService).GetMethod("LookupCountryCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initMethod = typeof(RegisterProcessingService).GetMethod("InitializeCountryLookupAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Initialize lookup
        await (Task)initMethod!.Invoke(_service, [CancellationToken.None])!;
        
        // Test IsoAlpha2 lookup
        var result = (short)method!.Invoke(_service, ["RU"])!;
        Assert.That(result, Is.EqualTo(643));
        
        result = (short)method.Invoke(_service, ["UZ"])!;
        Assert.That(result, Is.EqualTo(860));
        
        result = (short)method.Invoke(_service, ["US"])!;
        Assert.That(result, Is.EqualTo(840));
        
        result = (short)method.Invoke(_service, ["CN"])!;
        Assert.That(result, Is.EqualTo(156));
        
        result = (short)method.Invoke(_service, ["DE"])!;
        Assert.That(result, Is.EqualTo(276));
    }

    [Test]
    public async Task LookupCountryCode_ByNameRuShort_ReturnsCorrectCode()
    {
        var method = typeof(RegisterProcessingService).GetMethod("LookupCountryCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initMethod = typeof(RegisterProcessingService).GetMethod("InitializeCountryLookupAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Initialize lookup
        await (Task)initMethod!.Invoke(_service, [CancellationToken.None])!;
        
        // Test NameRuShort lookup
        var result = (short)method!.Invoke(_service, ["Российская Федерация"])!;
        Assert.That(result, Is.EqualTo(643));
        
        result = (short)method.Invoke(_service, ["Узбекистан"])!;
        Assert.That(result, Is.EqualTo(860));
        
        result = (short)method.Invoke(_service, ["США"])!;
        Assert.That(result, Is.EqualTo(840));
        
        result = (short)method.Invoke(_service, ["Китай"])!;
        Assert.That(result, Is.EqualTo(156));
        
        result = (short)method.Invoke(_service, ["Германия"])!;
        Assert.That(result, Is.EqualTo(276));
    }

    [Test]
    public async Task LookupCountryCode_ByIsoNumeric_ReturnsCorrectCode()
    {
        var method = typeof(RegisterProcessingService).GetMethod("LookupCountryCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initMethod = typeof(RegisterProcessingService).GetMethod("InitializeCountryLookupAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Initialize lookup
        await (Task)initMethod!.Invoke(_service, [CancellationToken.None])!;
        
        // Test IsoNumeric lookup (as string)
        var result = (short)method!.Invoke(_service, ["643"])!;
        Assert.That(result, Is.EqualTo(643));
        
        result = (short)method.Invoke(_service, ["860"])!;
        Assert.That(result, Is.EqualTo(860));
        
        result = (short)method.Invoke(_service, ["840"])!;
        Assert.That(result, Is.EqualTo(840));
        
        result = (short)method.Invoke(_service, ["156"])!;
        Assert.That(result, Is.EqualTo(156));
        
        result = (short)method.Invoke(_service, ["276"])!;
        Assert.That(result, Is.EqualTo(276));
    }

    [Test]
    public async Task LookupCountryCode_SpecialRussiaValue_ReturnsCorrectCode()
    {
        var method = typeof(RegisterProcessingService).GetMethod("LookupCountryCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initMethod = typeof(RegisterProcessingService).GetMethod("InitializeCountryLookupAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Initialize lookup
        await (Task)initMethod!.Invoke(_service, [CancellationToken.None])!;
        
        // Test special "Россия" value (should map to Russia's ISO numeric code 643)
        var result = (short)method!.Invoke(_service, ["Россия"])!;
        Assert.That(result, Is.EqualTo(643));
    }

    [Test]
    public async Task LookupCountryCode_CaseInsensitive_ReturnsCorrectCode()
    {
        var method = typeof(RegisterProcessingService).GetMethod("LookupCountryCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initMethod = typeof(RegisterProcessingService).GetMethod("InitializeCountryLookupAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Initialize lookup
        await (Task)initMethod!.Invoke(_service, [CancellationToken.None])!;
        
        // Test case insensitive lookup for IsoAlpha2 codes
        var result = (short)method!.Invoke(_service, ["ru"])!;
        Assert.That(result, Is.EqualTo(643));
        
        result = (short)method.Invoke(_service, ["uz"])!;
        Assert.That(result, Is.EqualTo(860));
        
        result = (short)method.Invoke(_service, ["us"])!;
        Assert.That(result, Is.EqualTo(840));
        
        result = (short)method.Invoke(_service, ["cn"])!;
        Assert.That(result, Is.EqualTo(156));
        
        result = (short)method.Invoke(_service, ["de"])!;
        Assert.That(result, Is.EqualTo(276));
    }

    [Test]
    public async Task LookupCountryCode_WithWhitespace_ReturnsCorrectCode()
    {
        var method = typeof(RegisterProcessingService).GetMethod("LookupCountryCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initMethod = typeof(RegisterProcessingService).GetMethod("InitializeCountryLookupAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Initialize lookup
        await (Task)initMethod!.Invoke(_service, [CancellationToken.None])!;
        
        // Test lookup with leading/trailing whitespace
        var result = (short)method!.Invoke(_service, [" RU "])!;
        Assert.That(result, Is.EqualTo(643));
        
        result = (short)method.Invoke(_service, ["\tUZ\t"])!;
        Assert.That(result, Is.EqualTo(860));
        
        result = (short)method.Invoke(_service, ["  643  "])!;
        Assert.That(result, Is.EqualTo(643));
    }

    [Test]
    public async Task LookupCountryCode_InvalidValue_ReturnsZero()
    {
        var method = typeof(RegisterProcessingService).GetMethod("LookupCountryCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initMethod = typeof(RegisterProcessingService).GetMethod("InitializeCountryLookupAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Initialize lookup
        await (Task)initMethod!.Invoke(_service, [CancellationToken.None])!;
        
        // Test invalid values
        var result = (short)method!.Invoke(_service, ["INVALID"])!;
        Assert.That(result, Is.EqualTo(0));
        
        result = (short)method.Invoke(_service, ["999"])!;
        Assert.That(result, Is.EqualTo(0));
        
        result = (short)method.Invoke(_service, ["ZZ"])!;
        Assert.That(result, Is.EqualTo(0));
        
        result = (short)method.Invoke(_service, ["Несуществующая страна"])!;
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task LookupCountryCode_EmptyOrNullValue_ReturnsZero()
    {
        var method = typeof(RegisterProcessingService).GetMethod("LookupCountryCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initMethod = typeof(RegisterProcessingService).GetMethod("InitializeCountryLookupAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Initialize lookup
        await (Task)initMethod!.Invoke(_service, [CancellationToken.None])!;
        
        // Test empty and null values
        var result = (short)method!.Invoke(_service, [""])!;
        Assert.That(result, Is.EqualTo(0));
        
        result = (short)method.Invoke(_service, ["   "])!;
        Assert.That(result, Is.EqualTo(0));
        
        result = (short)method.Invoke(_service, [null])!;
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task LookupCountryCode_MixedInputTypes_AllWork()
    {
        var method = typeof(RegisterProcessingService).GetMethod("LookupCountryCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initMethod = typeof(RegisterProcessingService).GetMethod("InitializeCountryLookupAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Initialize lookup
        await (Task)initMethod!.Invoke(_service, [CancellationToken.None])!;
        
        // Test that all input types work for the same country (Russia)
        var resultAlpha2 = (short)method!.Invoke(_service, ["RU"])!;
        var resultNumeric = (short)method.Invoke(_service, ["643"])!;
        var resultNameRu = (short)method.Invoke(_service, ["Российская Федерация"])!;
        var resultSpecial = (short)method.Invoke(_service, ["Россия"])!;
        
        // All should return the same value (643 for Russia)
        Assert.That(resultAlpha2, Is.EqualTo(643));
        Assert.That(resultNumeric, Is.EqualTo(643));
        Assert.That(resultNameRu, Is.EqualTo(643));
        Assert.That(resultSpecial, Is.EqualTo(643));
    }

    [Test]
    public async Task InitializeCountryLookupAsync_CalledMultipleTimes_DoesNotReinitialize()
    {
        var initMethod = typeof(RegisterProcessingService).GetMethod("InitializeCountryLookupAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Initialize multiple times
        await (Task)initMethod!.Invoke(_service, [CancellationToken.None])!;
        await (Task)initMethod.Invoke(_service, [CancellationToken.None])!;
        await (Task)initMethod.Invoke(_service, [CancellationToken.None])!;
        
        // Should still work correctly
        var method = typeof(RegisterProcessingService).GetMethod("LookupCountryCode", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (short)method!.Invoke(_service, ["RU"])!;
        Assert.That(result, Is.EqualTo(643));
    }
}