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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using AutoMapper;
using NUnit.Framework;
using Moq;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Tests.Controllers.Parcels;

[TestFixture]
public class ParcelsControllerSortingTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<IParcelValidationService> _mockValidationService;
    private Mock<IParcelFeacnCodeLookupService> _mockFeacnLookupService;
    private IMorphologySearchService _morphologyService;
    private Mock<IRegisterProcessingService> _mockProcessingService;
    private Mock<IParcelIndPostGenerator> _mockIndPostGenerator;
    private ILogger<ParcelsController> _logger;
    private IUserInformationService _userService;
    private ParcelsController _controller;
    private User _logistUser;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"parcels_controller_sorting_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        // Add roles and users
        var logistRole = new Role { Id = 1, Name = "logist", Title = "Ëîãèñò" };
        _dbContext.Roles.Add(logistRole);

        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _logistUser = new User
        {
            Id = 1,
            Email = "logist@example.com",
            Password = hpw,
            FirstName = "Log",
            LastName = "User",
            UserRoles = [new UserRole { UserId = 1, RoleId = 1, Role = logistRole }]
        };
        _dbContext.Users.Add(_logistUser);

        // Add companies (Ozon is 1, WBR is 2)
        _dbContext.Companies.AddRange(
            new Company { Id = 1, Inn = "1", Name = "Ozon" },
            new Company { Id = 2, Inn = "2", Name = "WBR" }
        );
        _dbContext.SaveChanges();

        // Create mocks
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<ParcelsController>();
        _mockValidationService = new Mock<IParcelValidationService>();
        _mockFeacnLookupService = new Mock<IParcelFeacnCodeLookupService>();
        _mockProcessingService = new Mock<IRegisterProcessingService>();
        _mockIndPostGenerator = new Mock<IParcelIndPostGenerator>();
        _morphologyService = new MorphologySearchService();
        _userService = new UserInformationService(_dbContext);
        _controller = CreateController();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private ParcelsController CreateController()
    {
        var mockMapper = new Mock<IMapper>();
        return new ParcelsController(
            _mockHttpContextAccessor.Object,
            _dbContext,
            _userService,
            _logger,
            mockMapper.Object,
            _mockValidationService.Object,
            _mockFeacnLookupService.Object,
            _morphologyService,
            _mockProcessingService.Object,
            _mockIndPostGenerator.Object);
    }

    private void SetCurrentUserId(int id)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = id;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(ctx);
        _controller = CreateController();
    }

    #region WBR Order Sorting Tests

    [Test]
    public async Task GetWbrOrders_SortsById_Ascending()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "wbr_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "A" },
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, sortBy: "id", sortOrder: "asc");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(3));
        Assert.That(items[0].Id, Is.EqualTo(1));
        Assert.That(items[1].Id, Is.EqualTo(2));
        Assert.That(items[2].Id, Is.EqualTo(3));
    }

    [Test]
    public async Task GetWbrOrders_SortsById_Descending()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "wbr_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "A" },
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, sortBy: "id", sortOrder: "desc");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(3));
        Assert.That(items[0].Id, Is.EqualTo(3));
        Assert.That(items[1].Id, Is.EqualTo(2));
        Assert.That(items[2].Id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetWbrOrders_SortsByStatusId_Ascending()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "wbr_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 3, TnVed = "A" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 2, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, sortBy: "statusId", sortOrder: "asc");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(3));
        Assert.That(items[0].Id, Is.EqualTo(2)); // StatusId = 1
        Assert.That(items[1].Id, Is.EqualTo(3)); // StatusId = 2
        Assert.That(items[2].Id, Is.EqualTo(1)); // StatusId = 3
    }

    [Test]
    public async Task GetWbrOrders_SortsByCheckStatusId_Descending()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "wbr_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 101, TnVed = "A" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, CheckStatusId = 201, TnVed = "B" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, sortBy: "checkStatusId", sortOrder: "desc");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(3));
        Assert.That(items[0].Id, Is.EqualTo(2)); // CheckStatusId = 201
        Assert.That(items[1].Id, Is.EqualTo(1)); // CheckStatusId = 101
        Assert.That(items[2].Id, Is.EqualTo(3)); // CheckStatusId = 1
    }

    [Test]
    public async Task GetWbrOrders_SortsByTnVed_Ascending()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "wbr_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "XYZ" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "ABC" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "MNO" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, sortBy: "tnVed", sortOrder: "asc");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(3));
        Assert.That(items[0].Id, Is.EqualTo(2)); // TnVed = "ABC"
        Assert.That(items[1].Id, Is.EqualTo(3)); // TnVed = "MNO"
        Assert.That(items[2].Id, Is.EqualTo(1)); // TnVed = "XYZ"
    }

    [Test]
    public async Task GetWbrOrders_SortsByShk_Ascending()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "wbr_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, Shk = "C123" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, Shk = "A456" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, Shk = "B789" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, sortBy: "shk", sortOrder: "asc");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(3));
        Assert.That(items[0].Id, Is.EqualTo(2)); // Shk = "A456"
        Assert.That(items[1].Id, Is.EqualTo(3)); // Shk = "B789"
        Assert.That(items[2].Id, Is.EqualTo(1)); // Shk = "C123"
    }

    #endregion

    #region Ozon Order Sorting Tests

    [Test]
    public async Task GetOzonOrders_SortsById_Ascending()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 1, FileName = "ozon_test.xlsx" }; // Ozon
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new OzonParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "A" },
            new OzonParcel { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new OzonParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, sortBy: "id", sortOrder: "asc");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(3));
        Assert.That(items[0].Id, Is.EqualTo(1));
        Assert.That(items[1].Id, Is.EqualTo(2));
        Assert.That(items[2].Id, Is.EqualTo(3));
    }

    [Test]
    public async Task GetOzonOrders_SortsByStatusId_Descending()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 1, FileName = "ozon_test.xlsx" }; // Ozon
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new OzonParcel { Id = 1, RegisterId = 1, StatusId = 3, TnVed = "A" },
            new OzonParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new OzonParcel { Id = 3, RegisterId = 1, StatusId = 2, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, sortBy: "statusId", sortOrder: "desc");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(3));
        Assert.That(items[0].Id, Is.EqualTo(1)); // StatusId = 3
        Assert.That(items[1].Id, Is.EqualTo(3)); // StatusId = 2
        Assert.That(items[2].Id, Is.EqualTo(2)); // StatusId = 1
    }

    [Test]
    public async Task GetOzonOrders_SortsByPostingNumber_Ascending()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 1, FileName = "ozon_test.xlsx" }; // Ozon
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new OzonParcel { Id = 1, RegisterId = 1, StatusId = 1, PostingNumber = "C123" },
            new OzonParcel { Id = 2, RegisterId = 1, StatusId = 1, PostingNumber = "A456" },
            new OzonParcel { Id = 3, RegisterId = 1, StatusId = 1, PostingNumber = "B789" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, sortBy: "postingNumber", sortOrder: "asc");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(3));
        Assert.That(items[0].Id, Is.EqualTo(2)); // PostingNumber = "A456"
        Assert.That(items[1].Id, Is.EqualTo(3)); // PostingNumber = "B789"
        Assert.That(items[2].Id, Is.EqualTo(1)); // PostingNumber = "C123"
    }

    #endregion

    #region Filtering Tests

    [Test]
    public async Task GetOrders_FiltersByStatusId()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "filter_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 3, TnVed = "A" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 2, TnVed = "B" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 2, TnVed = "C" },
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 1, TnVed = "D" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, statusId: 2);
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0].Id, Is.EqualTo(2));
        Assert.That(items[1].Id, Is.EqualTo(3));
        Assert.That(items.All(i => i.StatusId == 2), Is.True);
    }

    [Test]
    public async Task GetOrders_FiltersByTnVed()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "filter_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "123456" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "123ABC" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "456DEF" },
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 1, TnVed = "789XYZ" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1, tnVed: "123");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0].Id, Is.EqualTo(1));
        Assert.That(items[1].Id, Is.EqualTo(2));
        Assert.That(items.All(i => i.TnVed!.Contains("123")), Is.True);
    }

    [Test]
    public async Task GetOrders_CombinesFiltersAndSorting()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "combined_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 2, TnVed = "123ABC" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 2, TnVed = "123XYZ" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "123DEF" },
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 2, TnVed = "456ABC" }
        );
        await _dbContext.SaveChangesAsync();

        // Act - Filter by statusId=2 and tnVed=123, sort by tnVed desc
        var result = await _controller.GetOrders(
            registerId: 1, 
            statusId: 2, 
            tnVed: "123", 
            sortBy: "tnVed", 
            sortOrder: "desc");
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0].Id, Is.EqualTo(2)); // TnVed = "123XYZ" comes first when sorting desc
        Assert.That(items[1].Id, Is.EqualTo(1)); // TnVed = "123ABC" comes second
        Assert.That(items.All(i => i.StatusId == 2 && i.TnVed!.Contains("123")), Is.True);
    }

    #endregion

    #region Pagination Tests

    [Test]
    public async Task GetOrders_PaginatesResults()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "pagination_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        
        // Add 15 orders
        for (int i = 1; i <= 15; i++)
        {
            _dbContext.Orders.Add(new WbrParcel 
            { 
                Id = i, 
                RegisterId = 1, 
                StatusId = 1, 
                TnVed = $"TnVed{i:D2}" 
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act - Get page 2 with page size 5
        var result = await _controller.GetOrders(registerId: 1, page: 2, pageSize: 5);
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(5));
        Assert.That(items[0].Id, Is.EqualTo(6));  // Second page should start with ID 6
        Assert.That(items[4].Id, Is.EqualTo(10)); // And end with ID 10
        
        Assert.That(pagedResult.Pagination.CurrentPage, Is.EqualTo(2));
        Assert.That(pagedResult.Pagination.PageSize, Is.EqualTo(5));
        Assert.That(pagedResult.Pagination.TotalCount, Is.EqualTo(15));
        Assert.That(pagedResult.Pagination.TotalPages, Is.EqualTo(3));
        Assert.That(pagedResult.Pagination.HasNextPage, Is.True);
        Assert.That(pagedResult.Pagination.HasPreviousPage, Is.True);
    }

    [Test]
    public async Task GetOrders_ReturnsAllItems_WhenPageSizeIsMinusOne()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "pagination_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        
        // Add 20 orders
        for (int i = 1; i <= 20; i++)
        {
            _dbContext.Orders.Add(new WbrParcel 
            { 
                Id = i, 
                RegisterId = 1, 
                StatusId = 1, 
                TnVed = $"TnVed{i:D2}" 
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act - Get all items with pageSize = -1
        var result = await _controller.GetOrders(registerId: 1, pageSize: -1);
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(20));
        Assert.That(pagedResult.Pagination.TotalCount, Is.EqualTo(20));
        Assert.That(pagedResult.Pagination.TotalPages, Is.EqualTo(1));
    }

    [Test]
    public async Task GetOrders_ResetsToFirstPage_WhenPageExceedsTotalPages()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "pagination_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        
        // Add 10 orders
        for (int i = 1; i <= 10; i++)
        {
            _dbContext.Orders.Add(new WbrParcel 
            { 
                Id = i, 
                RegisterId = 1, 
                StatusId = 1, 
                TnVed = $"TnVed{i:D2}" 
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act - Request page 5 when there are only 2 pages (pageSize = 5)
        var result = await _controller.GetOrders(registerId: 1, page: 5, pageSize: 5);
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(pagedResult.Pagination.CurrentPage, Is.EqualTo(1)); // Should reset to page 1
        Assert.That(items[0].Id, Is.EqualTo(1)); // Should show first page items
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task GetOrders_ReturnsBadRequest_WhenInvalidPagination()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "error_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        // Act - Try with page = 0 (invalid)
        var result = await _controller.GetOrders(registerId: 1, page: 0);
        
        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objResult = result.Result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetOrders_ReturnsBadRequest_WhenInvalidSortBy()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "error_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        // Act - Try with invalid sortBy field
        var result = await _controller.GetOrders(registerId: 1, sortBy: "nonExistentField");
        
        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objResult = result.Result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetOrders_ReturnsNotFound_WhenRegisterNotFound()
    {
        // Arrange
        SetCurrentUserId(1);
        
        // Act - Try with non-existent register ID
        var result = await _controller.GetOrders(registerId: 999);
        
        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objResult = result.Result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetOrders_ReturnsForbidden_WhenUserNotLogist()
    {
        // Arrange
        SetCurrentUserId(99); // Non-logist user
        var register = new Register { Id = 1, CompanyId = 2, FileName = "error_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrders(registerId: 1);
        
        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objResult = result.Result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    #endregion
}