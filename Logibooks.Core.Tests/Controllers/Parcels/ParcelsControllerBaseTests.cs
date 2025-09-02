// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Moq;
using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;

namespace Logibooks.Core.Tests.Controllers.Parcels;

[TestFixture]
public class ParcelsControllerBaseTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger> _mockLogger;
    private TestParcelsController _controller;
    private Register _register;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"parcels_base_controller_db_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        // Set up test data
        _dbContext.Companies.AddRange(
            new Company { Id = 1, Inn = "1", Name = "Ozon" },
            new Company { Id = 2, Inn = "2", Name = "WBR" }
        );
        
        _register = new Register { Id = 1, CompanyId = 2, FileName = "test.xlsx" };
        _dbContext.Registers.Add(_register);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger>();
        _controller = new TestParcelsController(_mockHttpContextAccessor.Object, _dbContext, _mockLogger.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelNotFound_ReturnsFirstParcelBySort()
    {
        // Arrange: Create parcels with different status IDs
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 3, CheckStatusId = 1, TnVed = "A" },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "B" },
            new WbrParcel { Id = 30, RegisterId = 1, StatusId = 2, CheckStatusId = 1, TnVed = "C" }
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Try to get next parcel from a non-existent parcel (ID 999)
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 999, // This parcel doesn't exist
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "statusid",
            sortOrder: "asc",
            withIssues: false
        );

        // Assert: Should return the first parcel when sorted by statusid ascending (ID 20, StatusId = 1)
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(20));
        Assert.That(result.StatusId, Is.EqualTo(1));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelFilteredOut_ReturnsNextFilteredParcelAfterCurrent()
    {
        // Arrange: Create parcels with different status and check status IDs
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "A" },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 2, CheckStatusId = 1, TnVed = "B" },
            new WbrParcel { Id = 30, RegisterId = 1, StatusId = 3, CheckStatusId = 2, TnVed = "C" }, // Filtered out
            new WbrParcel { Id = 40, RegisterId = 1, StatusId = 4, CheckStatusId = 1, TnVed = "D" }  // Next match after 30
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Try to get next parcel from parcel ID 30, but filter by CheckStatusId = 1 (which excludes parcel 30)
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 30, // This parcel exists but is filtered out by checkStatusId filter
            statusId: null,
            checkStatusId: 1, // This filters out parcel 30 which has CheckStatusId = 2
            tnVed: null,
            sortBy: "id",
            sortOrder: "asc",
            withIssues: false
        );

        // Assert: Should return the next parcel after ID 30 that matches the filter (ID 40)
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(40));
        Assert.That(result.CheckStatusId, Is.EqualTo(1));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelFilteredOut_WithDescendingSort()
    {
        // Arrange: Create parcels with different TN VED values
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "ALPHA" },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "BETA" },
            new WbrParcel { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "GAMMA" }
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Try to get next parcel from a non-existent parcel, sorted by TN VED descending
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 999, // Non-existent parcel
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "tnved",
            sortOrder: "desc",
            withIssues: false
        );

        // Assert: Should return the first parcel when sorted by tnved descending (GAMMA comes first)
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(30));
        Assert.That(result.TnVed, Is.EqualTo("GAMMA"));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelFilteredOut_WithTnVedFilter()
    {
        // Arrange: Create parcels with different TN VED values
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "123ABC" },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "456DEF" }, // Filtered out  
            new WbrParcel { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "123XYZ" }  // Next match after 20
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Try to get next parcel from parcel ID 20, but filter by TnVed containing "123" (which excludes parcel 20)
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 20, // This parcel exists but is filtered out by tnVed filter
            statusId: null,
            checkStatusId: null,
            tnVed: "123", // This filters out parcel 20 which has "456DEF"
            sortBy: "id",
            sortOrder: "asc",
            withIssues: false
        );

        // Assert: Should return the next parcel after ID 20 that matches the filter (ID 30)
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(30));
        Assert.That(result.TnVed, Does.Contain("123"));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelFilteredOut_WithFeacnLookupSort()
    {
        // Arrange: Create parcels and FEACN data for sorting
        var feacnCodes = new[]
        {
            new FeacnCode { 
                Id = 1, 
                Code = "1234567890", 
                CodeEx = "1234567890", 
                Name = "Test FEACN Code", 
                NormalizedName = "test feacn code" 
            }
        };
        _dbContext.FeacnCodes.AddRange(feacnCodes);

        var keyword = new KeyWord { Id = 1, Word = "test", MatchTypeId = 1 };
        keyword.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 1, FeacnCode = "1234567890", KeyWord = keyword }];
        _dbContext.KeyWords.Add(keyword);

        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "1234567890" }, // Will have priority 1 when keywords are added
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "9999999999" }, // Will have priority 8 (no keywords, TnVed not in DB)
            new WbrParcel { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "0000000000" }  // Will have priority 8
        };
        _dbContext.Parcels.AddRange(parcels);

        // Add keyword link to first parcel to give it priority 1
        var keywordLink = new BaseParcelKeyWord { BaseParcelId = 10, KeyWordId = 1, BaseParcel = parcels[0], KeyWord = keyword };
        _dbContext.Set<BaseParcelKeyWord>().Add(keywordLink);

        await _dbContext.SaveChangesAsync();

        // Act: Try to get next parcel from a non-existent parcel, sorted by feacnlookup ascending (best match first)
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 999, // Non-existent parcel
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "feacnlookup",
            sortOrder: "asc",
            withIssues: false
        );

        // Assert: Should return the parcel with the best FEACN match (priority 1)
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(10));
        Assert.That(result.TnVed, Is.EqualTo("1234567890"));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelFilteredOut_WithOzonParcels()
    {
        // Arrange: Create Ozon register and parcels
        var ozonRegister = new Register { Id = 2, CompanyId = 1, FileName = "ozon.xlsx" }; // CompanyId = 1 is Ozon
        _dbContext.Registers.Add(ozonRegister);

        var parcels = new[]
        {
            new OzonParcel { Id = 100, RegisterId = 2, StatusId = 1, CheckStatusId = 1, PostingNumber = "POST001" },
            new OzonParcel { Id = 200, RegisterId = 2, StatusId = 2, CheckStatusId = 1, PostingNumber = "POST002" },
            new OzonParcel { Id = 300, RegisterId = 2, StatusId = 3, CheckStatusId = 1, PostingNumber = "POST003" }
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Try to get next parcel from a non-existent parcel, sorted by postingnumber ascending
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 1, // Ozon
            registerId: 2,
            parcelId: 999, // Non-existent parcel
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "postingnumber",
            sortOrder: "asc",
            withIssues: false
        );

        // Assert: Should return the first parcel when sorted by postingnumber ascending
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(100));
        var ozonParcel = result as OzonParcel;
        Assert.That(ozonParcel!.PostingNumber, Is.EqualTo("POST001"));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelFilteredOut_WithWithIssuesFilter()
    {
        // Arrange: Create parcels with different check status IDs
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NotChecked }, // Below HasIssues threshold
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.HasIssues }, // In HasIssues range
            new WbrParcel { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.InvalidFeacnFormat }, // In HasIssues range
            new WbrParcel { Id = 40, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NoIssues } // Above HasIssues range
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Try to get next parcel from parcel ID 10 with withIssues filter (which excludes parcel 10)
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 10, // This parcel exists but is filtered out by withIssues filter
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "id",
            sortOrder: "asc",
            withIssues: true // This filters to only parcels with CheckStatusId >= HasIssues and < NoIssues
        );

        // Assert: Should return the first parcel with issues (ID 20)
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(20));
        Assert.That(result.CheckStatusId, Is.GreaterThanOrEqualTo((int)ParcelCheckStatusCode.HasIssues));
        Assert.That(result.CheckStatusId, Is.LessThan((int)ParcelCheckStatusCode.NoIssues));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelFilteredOut_NoMatchingParcels_ReturnsNull()
    {
        // Arrange: Create parcels that won't match the filter
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "ABC" },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 2, CheckStatusId = 1, TnVed = "DEF" }
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Try to get next parcel with a filter that matches no parcels
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 999, // Non-existent parcel
            statusId: 999, // Status that doesn't exist - will match no parcels
            checkStatusId: null,
            tnVed: null,
            sortBy: "id",
            sortOrder: "asc",
            withIssues: false
        );

        // Assert: Should return null when no parcels match the filter
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelFilteredOut_InvalidSortBy_ReturnsNull()
    {
        // Arrange: Create a parcel
        var parcel = new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1 };
        _dbContext.Parcels.Add(parcel);
        await _dbContext.SaveChangesAsync();

        // Act: Try to get next parcel with invalid sortBy
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 999, // Non-existent parcel
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "invalidfield", // Invalid sort field
            sortOrder: "asc",
            withIssues: false
        );

        // Assert: Should return null for invalid sortBy
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelExists_ReturnsNextParcel()
    {
        // Arrange: Create parcels in sequence
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1 },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 2, CheckStatusId = 1 },
            new WbrParcel { Id = 30, RegisterId = 1, StatusId = 3, CheckStatusId = 1 }
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Get next parcel after parcel ID 10 (which exists and should have normal keyset behavior)
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 10, // This parcel exists and should follow normal keyset pagination
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "statusid",
            sortOrder: "asc",
            withIssues: false
        );

        // Assert: Should return the next parcel in status order (ID 20, StatusId = 2)
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(20));
        Assert.That(result.StatusId, Is.EqualTo(2));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_CurrentParcelIsLast_ReturnsNull()
    {
        // Arrange: Create parcels
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1 },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 2, CheckStatusId = 1 }
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Get next parcel after the last parcel (ID 20)
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 20, // This is the last parcel when sorted by statusid asc
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "statusid",
            sortOrder: "asc",
            withIssues: false
        );

        // Assert: Should return null when there are no more parcels
        Assert.That(result, Is.Null);
    }

    [TestCase("asc", 10, 20)]
    [TestCase("desc", 20, 10)]
    public async Task GetNextParcelKeysetAsync_CheckStatusKeysetPredicate_Works(string sortOrder, int startId, int expectedId)
    {
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1 },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 2 },
            new WbrParcel { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 3 }
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2,
            registerId: 1,
            parcelId: startId,
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "checkstatusid",
            sortOrder: sortOrder,
            withIssues: false
        );

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(expectedId));
    }

    [TestCase("asc", 10, 20)]
    [TestCase("desc", 20, 10)]
    public async Task GetNextParcelKeysetAsync_ShkKeysetPredicate_Works(string sortOrder, int startId, int expectedId)
    {
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1, Shk = "100" },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 1, Shk = "200" },
            new WbrParcel { Id = 30, RegisterId = 1, StatusId = 1, CheckStatusId = 1, Shk = "300" }
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2,
            registerId: 1,
            parcelId: startId,
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "shk",
            sortOrder: sortOrder,
            withIssues: false
        );

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(expectedId));
    }

    [TestCase("asc")]
    [TestCase("desc")]
    public async Task GetNextParcelKeysetAsync_FeacnKeysetPredicate_Works(string sortOrder)
    {
        _dbContext.FeacnCodes.Add(new FeacnCode
        {
            Id = 1,
            Code = "1234567890",
            CodeEx = "1234567890",
            Name = "Test",
            NormalizedName = "test"
        });

        var keyword = new KeyWord { Id = 1, Word = "test", MatchTypeId = 1 };
        keyword.KeyWordFeacnCodes = [new KeyWordFeacnCode { KeyWordId = 1, FeacnCode = "1234567890", KeyWord = keyword }];

        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "1234567890" },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "0000000000" }
        };

        var keywordLink = new BaseParcelKeyWord { BaseParcelId = 10, KeyWordId = 1, BaseParcel = parcels[0], KeyWord = keyword };

        _dbContext.Parcels.AddRange(parcels);
        _dbContext.Set<BaseParcelKeyWord>().Add(keywordLink);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2,
            registerId: 1,
            parcelId: 10,
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "feacnlookup",
            sortOrder: sortOrder,
            withIssues: false
        );

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(20));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_StatusIdDescending_Works()
    {
        var parcels = new[]
        {
            new WbrParcel { Id = 10, RegisterId = 1, StatusId = 1, CheckStatusId = 1 },
            new WbrParcel { Id = 20, RegisterId = 1, StatusId = 2, CheckStatusId = 1 },
            new WbrParcel { Id = 30, RegisterId = 1, StatusId = 3, CheckStatusId = 1 }
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2,
            registerId: 1,
            parcelId: 20,
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "statusid",
            sortOrder: "desc",
            withIssues: false
        );

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(10));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_OzonPostingNumberDescending_Works()
    {
        var ozonRegister = new Register { Id = 2, CompanyId = 1, FileName = "ozon.xlsx" };
        _dbContext.Registers.Add(ozonRegister);

        var parcels = new[]
        {
            new OzonParcel { Id = 100, RegisterId = 2, StatusId = 1, CheckStatusId = 1, PostingNumber = "POST001" },
            new OzonParcel { Id = 200, RegisterId = 2, StatusId = 1, CheckStatusId = 1, PostingNumber = "POST002" },
            new OzonParcel { Id = 300, RegisterId = 2, StatusId = 1, CheckStatusId = 1, PostingNumber = "POST003" }
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 1,
            registerId: 2,
            parcelId: 200,
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "postingnumber",
            sortOrder: "desc",
            withIssues: false
        );

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(100));
    }

    [TestCase(new[] { "A" }, "A", new[] { "A" }, 1)]
    [TestCase(new[] { "A", "B" }, "B", new string[0], 2)]
    [TestCase(new[] { "A" }, "B", new[] { "B" }, 3)]
    [TestCase(new[] { "A", "B" }, "C", new[] { "C" }, 4)]
    [TestCase(new[] { "A" }, "B", new string[0], 5)]
    [TestCase(new[] { "A", "B" }, "C", new string[0], 6)]
    [TestCase(new string[0], "A", new[] { "A" }, 7)]
    [TestCase(new string[0], "A", new string[0], 8)]
    public async Task CalculateMatchPriorityAsync_ReturnsExpectedPriority(string[] keywordCodes, string tnVed, string[] dbCodes, int expected)
    {
        foreach (var code in dbCodes)
        {
            _dbContext.FeacnCodes.Add(new FeacnCode
            {
                Code = code,
                CodeEx = code,
                Name = code,
                NormalizedName = code
            });
        }
        _dbContext.SaveChanges();

        var parcel = new WbrParcel { TnVed = tnVed, BaseParcelKeyWords = new List<BaseParcelKeyWord>() };
        if (keywordCodes.Length > 0)
        {
            var kw = new KeyWord { Id = 1, Word = "kw", MatchTypeId = 1, KeyWordFeacnCodes = new List<KeyWordFeacnCode>() };
            foreach (var code in keywordCodes)
            {
                kw.KeyWordFeacnCodes.Add(new KeyWordFeacnCode { KeyWordId = 1, FeacnCode = code, KeyWord = kw });
            }
            parcel.BaseParcelKeyWords.Add(new BaseParcelKeyWord { BaseParcelId = 0, BaseParcel = parcel, KeyWordId = 1, KeyWord = kw });
        }

        var priority = await _controller.TestCalculateMatchPriorityAsync(parcel);
        Assert.That(priority, Is.EqualTo(expected));
    }
}

/// <summary>
/// Test implementation of ParcelsControllerBase to expose protected methods for testing
/// </summary>
public class TestParcelsController : ParcelsControllerBase
{
    public TestParcelsController(IHttpContextAccessor httpContextAccessor, AppDbContext db, ILogger logger)
        : base(httpContextAccessor, db, logger)
    {
    }

    /// <summary>
    /// Expose GetNextParcelKeysetAsync for testing
    /// </summary>
    public Task<BaseParcel?> TestGetNextParcelKeysetAsync(
        int companyId,
        int registerId,
        int parcelId,
        int? statusId,
        int? checkStatusId,
        string? tnVed,
        string sortBy,
        string sortOrder,
        bool withIssues)
    {
        return GetNextParcelKeysetAsync(companyId, registerId, parcelId, statusId, checkStatusId, tnVed, sortBy, sortOrder, withIssues);
    }

    public Task<int> TestCalculateMatchPriorityAsync(BaseParcel parcel)
    {
        var method = typeof(ParcelsControllerBase).GetMethod("CalculateMatchPriorityAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        return (Task<int>)method!.Invoke(this, new object[] { parcel })!;
    }
}