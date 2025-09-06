// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

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
public class ParcelsControllerNewBehaviorTests
{
    private AppDbContext _dbContext = null!;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor = null!;
    private Mock<ILogger> _mockLogger = null!;
    private TestParcelsController _controller = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"new_behavior_test_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _dbContext.Companies.AddRange(
            new Company { Id = 1, Inn = "1", Name = "Ozon" },
            new Company { Id = 2, Inn = "2", Name = "WBR" }
        );
        
        var register = new Register { Id = 1, CompanyId = 2, FileName = "test.xlsx" };
        _dbContext.Registers.Add(register);
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
    public async Task GetNextParcelKeysetAsync_WithIssuesFilter_ReturnsNextParcelWithIssuesAfterCurrent()
    {
        // Arrange: Scenario described by user
        // Parcels: [1(no issues), 2(has issues), 3(no issues), 4(has issues)]
        // Current: parcel 1, withIssues=true
        // Expected: parcel 2 (next with issues after current)
        var parcels = new[]
        {
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NotChecked }, // No issues
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.HasIssues }, // Has issues
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NotChecked }, // No issues  
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.HasIssues }  // Has issues
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Get next parcel with issues after parcel 1 (which has no issues)
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 1, // Current parcel has no issues, so it's filtered out
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "id",
            sortOrder: "asc",
            withIssues: true // Filter to only parcels with issues
        );

        // Assert: Should return parcel 2 (next parcel with issues after parcel 1)
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(2));
        Assert.That(result.CheckStatusId, Is.GreaterThanOrEqualTo((int)ParcelCheckStatusCode.HasIssues));
        Assert.That(result.CheckStatusId, Is.LessThan((int)ParcelCheckStatusCode.NoIssues));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_WithIssuesFilter_SkipsToNextIssueParcel()
    {
        // Arrange: More complex scenario
        // Parcels: [1(no issues), 2(no issues), 3(no issues), 4(has issues)]
        // Current: parcel 1, withIssues=true  
        // Expected: parcel 4 (first parcel with issues after current)
        var parcels = new[]
        {
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NotChecked }, // No issues
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NotChecked }, // No issues
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NotChecked }, // No issues
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.HasIssues }  // Has issues
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Get next parcel with issues after parcel 1
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 1, // Current parcel has no issues
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "id",
            sortOrder: "asc",
            withIssues: true
        );

        // Assert: Should skip parcels 2 and 3 and return parcel 4
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(4));
        Assert.That(result.CheckStatusId, Is.GreaterThanOrEqualTo((int)ParcelCheckStatusCode.HasIssues));
    }

    [Test]
    public async Task GetNextParcelKeysetAsync_WithIssuesFilter_NoMoreIssuesAfterCurrent_ReturnsNull()
    {
        // Arrange: No parcels with issues after current
        // Parcels: [1(no issues), 2(has issues), 3(no issues)]
        // Current: parcel 2, withIssues=true (parcel 2 has issues but we want NEXT)
        // Expected: null (no more parcels with issues after parcel 2)
        var parcels = new[]
        {
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NotChecked }, // No issues
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.HasIssues }, // Has issues
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = (int)ParcelCheckStatusCode.NotChecked }  // No issues
        };
        _dbContext.Parcels.AddRange(parcels);
        await _dbContext.SaveChangesAsync();

        // Act: Get next parcel with issues after parcel 2 (the only one with issues)
        var result = await _controller.TestGetNextParcelKeysetAsync(
            companyId: 2, // WBR
            registerId: 1,
            parcelId: 2, // This parcel has issues, so it's in the filtered set
            statusId: null,
            checkStatusId: null,
            tnVed: null,
            sortBy: "id",
            sortOrder: "asc",
            withIssues: true
        );

        // Assert: Should return null (no more parcels with issues after parcel 2)
        Assert.That(result, Is.Null);
    }
}