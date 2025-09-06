// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

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
        _mockFeacnLookupService.Setup(s => s.LookupAsync(
            It.IsAny<BaseParcel>(),
            It.IsAny<MorphologyContext>(),
            It.IsAny<WordsLookupContext<KeyWord>>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new List<int>());
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
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "A" },
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, sortBy: "id", sortOrder: "asc");
        
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
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "A" },
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, sortBy: "id", sortOrder: "desc");
        
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
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 3, TnVed = "A" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 2, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, sortBy: "statusId", sortOrder: "asc");
        
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
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 101, TnVed = "A" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, CheckStatusId = 201, TnVed = "B" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 1, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, sortBy: "checkStatusId", sortOrder: "desc");
        
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
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "XYZ" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "ABC" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "MNO" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, sortBy: "tnVed", sortOrder: "asc");
        
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
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, Shk = "C123" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, Shk = "A456" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, Shk = "B789" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, sortBy: "shk", sortOrder: "asc");
        
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
        _dbContext.Parcels.AddRange(
            new OzonParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "A" },
            new OzonParcel { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new OzonParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, sortBy: "id", sortOrder: "asc");
        
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
        _dbContext.Parcels.AddRange(
            new OzonParcel { Id = 1, RegisterId = 1, StatusId = 3, TnVed = "A" },
            new OzonParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "B" },
            new OzonParcel { Id = 3, RegisterId = 1, StatusId = 2, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, sortBy: "statusId", sortOrder: "desc");
        
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
        _dbContext.Parcels.AddRange(
            new OzonParcel { Id = 1, RegisterId = 1, StatusId = 1, PostingNumber = "C123" },
            new OzonParcel { Id = 2, RegisterId = 1, StatusId = 1, PostingNumber = "A456" },
            new OzonParcel { Id = 3, RegisterId = 1, StatusId = 1, PostingNumber = "B789" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, sortBy: "postingNumber", sortOrder: "asc");
        
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
    public async Task GetParcels_FiltersByStatusId()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "filter_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 3, TnVed = "A" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 2, TnVed = "B" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 2, TnVed = "C" },
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 1, TnVed = "D" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, statusId: 2);
        
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
    public async Task GetParcels_FiltersByTnVed()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "filter_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, TnVed = "123456" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, TnVed = "123ABC" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, TnVed = "456DEF" },
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 1, TnVed = "789XYZ" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, tnVed: "123");
        
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
    public async Task GetParcels_FiltersByCheckStatusId()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "filter_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 101, TnVed = "A" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1, CheckStatusId = 102, TnVed = "B" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 102, TnVed = "C" },
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 1, CheckStatusId = 103, TnVed = "D" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, checkStatusId: 102);
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0].Id, Is.EqualTo(2));
        Assert.That(items[1].Id, Is.EqualTo(3));
        Assert.That(items.All(i => i.CheckStatusId == 102), Is.True);
    }

    [Test]
    public async Task GetParcels_FiltersByCheckStatusId_ForOzonParcels()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 1, FileName = "filter_test.xlsx" }; // Ozon
        _dbContext.Registers.Add(register);
        _dbContext.Parcels.AddRange(
            new OzonParcel { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 201, TnVed = "A" },
            new OzonParcel { Id = 2, RegisterId = 1, StatusId = 1, CheckStatusId = 202, TnVed = "B" },
            new OzonParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 201, TnVed = "C" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1, checkStatusId: 201);
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0].Id, Is.EqualTo(1));
        Assert.That(items[1].Id, Is.EqualTo(3));
        Assert.That(items.All(i => i.CheckStatusId == 201), Is.True);
    }

    [Test]
    public async Task GetParcels_CombinesFiltersAndSorting()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "combined_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 2, CheckStatusId = 102, TnVed = "123ABC" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 2, CheckStatusId = 102, TnVed = "123XYZ" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 102, TnVed = "123DEF" },
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 2, CheckStatusId = 103, TnVed = "123GHI" },
            new WbrParcel { Id = 5, RegisterId = 1, StatusId = 2, CheckStatusId = 102, TnVed = "456JKL" }
        );
        await _dbContext.SaveChangesAsync();

        // Act - Filter by statusId=2, checkStatusId=102, tnVed=123, sort by tnVed desc
        var result = await _controller.GetParcels(
            registerId: 1, 
            statusId: 2, 
            checkStatusId: 102,
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
        Assert.That(items.All(i => i.StatusId == 2 && i.CheckStatusId == 102 && i.TnVed!.Contains("123")), Is.True);
    }

    [Test]
    public async Task GetParcels_CombinesStatusIdAndCheckStatusIdFilters()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "combined_filter_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 2, CheckStatusId = 102, TnVed = "A" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 2, CheckStatusId = 103, TnVed = "B" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 102, TnVed = "C" },
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 2, CheckStatusId = 102, TnVed = "D" }
        );
        await _dbContext.SaveChangesAsync();

        // Act - Filter by statusId=2 and checkStatusId=102
        var result = await _controller.GetParcels(registerId: 1, statusId: 2, checkStatusId: 102);
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0].Id, Is.EqualTo(1));
        Assert.That(items[1].Id, Is.EqualTo(4));
        Assert.That(items.All(i => i.StatusId == 2 && i.CheckStatusId == 102), Is.True);
    }

    [Test]
    public async Task GetParcels_CombinesAllFiltersWithSorting()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "all_filters_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        _dbContext.Parcels.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 2, CheckStatusId = 102, TnVed = "123ABC" },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 2, CheckStatusId = 102, TnVed = "123XYZ" },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 102, TnVed = "123DEF" },
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 2, CheckStatusId = 103, TnVed = "123GHI" },
            new WbrParcel { Id = 5, RegisterId = 1, StatusId = 2, CheckStatusId = 102, TnVed = "456JKL" }
        );
        await _dbContext.SaveChangesAsync();

        // Act - Filter by statusId=2, checkStatusId=102, tnVed=123, sort by tnVed desc
        var result = await _controller.GetParcels(
            registerId: 1, 
            statusId: 2, 
            checkStatusId: 102,
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
        Assert.That(items.All(i => i.StatusId == 2 && i.CheckStatusId == 102 && i.TnVed!.Contains("123")), Is.True);
    }

    #endregion

    #region Pagination Tests

    [Test]
    public async Task GetParcels_PaginatesResults()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "pagination_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        
        // Add 15 orders
        for (int i = 1; i <= 15; i++)
        {
            _dbContext.Parcels.Add(new WbrParcel 
            { 
                Id = i, 
                RegisterId = 1, 
                StatusId = 1, 
                TnVed = $"TnVed{i:D2}" 
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act - Get page 2 with page size 5
        var result = await _controller.GetParcels(registerId: 1, page: 2, pageSize: 5);
        
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
    public async Task GetParcels_ReturnsAllItems_WhenPageSizeIsMinusOne()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "pagination_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        
        // Add 20 orders
        for (int i = 1; i <= 20; i++)
        {
            _dbContext.Parcels.Add(new WbrParcel 
            { 
                Id = i, 
                RegisterId = 1, 
                StatusId = 1, 
                TnVed = $"TnVed{i:D2}" 
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act - Get all items with pageSize = -1
        var result = await _controller.GetParcels(registerId: 1, pageSize: -1);
        
        // Assert
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        var items = pagedResult!.Items.ToArray();
        
        Assert.That(items.Length, Is.EqualTo(20));
        Assert.That(pagedResult.Pagination.TotalCount, Is.EqualTo(20));
        Assert.That(pagedResult.Pagination.TotalPages, Is.EqualTo(1));
    }

    [Test]
    public async Task GetParcels_ResetsToFirstPage_WhenPageExceedsTotalPages()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "pagination_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        
        // Add 10 orders
        for (int i = 1; i <= 10; i++)
        {
            _dbContext.Parcels.Add(new WbrParcel 
            { 
                Id = i, 
                RegisterId = 1, 
                StatusId = 1, 
                TnVed = $"TnVed{i:D2}" 
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act - Request page 5 when there are only 2 pages (pageSize = 5)
        var result = await _controller.GetParcels(registerId: 1, page: 5, pageSize: 5);
        
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
    public async Task GetParcels_ReturnsBadRequest_WhenInvalidPagination()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "error_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        // Act - Try with page = 0 (invalid)
        var result = await _controller.GetParcels(registerId: 1, page: 0);
        
        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objResult = result.Result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetParcels_ReturnsBadRequest_WhenInvalidSortBy()
    {
        // Arrange
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "error_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        // Act - Try with invalid sortBy field
        var result = await _controller.GetParcels(registerId: 1, sortBy: "nonExistentField");
        
        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objResult = result.Result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetParcels_ReturnsNotFound_WhenRegisterNotFound()
    {
        // Arrange
        SetCurrentUserId(1);
        
        // Act - Try with non-existent register ID
        var result = await _controller.GetParcels(registerId: 999);
        
        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objResult = result.Result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetParcels_ReturnsForbidden_WhenUserNotLogist()
    {
        // Arrange
        SetCurrentUserId(99); // Non-logist user
        var register = new Register { Id = 1, CompanyId = 2, FileName = "error_test.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetParcels(registerId: 1);
        
        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objResult = result.Result as ObjectResult;
        Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    #endregion

    #region Sort by Match Tests

    [Test]
    public async Task GetParcels_SortsByMatch_Ascending_WithCorrectEightPriorities()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(register);

        // Create test FeacnCodes
        var feacnCode1 = new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "", Name = "", NormalizedName = "" };
        var feacnCode2 = new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "", Name = "", NormalizedName = "" };
        var feacnCode3 = new FeacnCode { Id = 3, Code = "3333333333", CodeEx = "", Name = "", NormalizedName = "" };
        var feacnCode4 = new FeacnCode { Id = 4, Code = "4444444444", CodeEx = "", Name = "", NormalizedName = "" };
        _dbContext.FeacnCodes.AddRange(feacnCode1, feacnCode2, feacnCode3, feacnCode4);

        // Create Keywords
        var keyword1 = new KeyWord { Id = 101, Word = "gold", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }; // Single FeacnCode, matches TnVed
        var keyword2 = new KeyWord { Id = 102, Word = "silver", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }; // Multiple FeacnCodes, matches TnVed
        var keyword3 = new KeyWord { Id = 103, Word = "bronze", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }; // Single FeacnCode, no TnVed match, TnVed exists
        var keyword4 = new KeyWord { Id = 104, Word = "copper", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }; // Multiple FeacnCodes, no TnVed match, TnVed exists
        var keyword5 = new KeyWord { Id = 105, Word = "iron", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }; // Single FeacnCode, no TnVed match, TnVed not exists
        var keyword6 = new KeyWord { Id = 106, Word = "zinc", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }; // Multiple FeacnCodes, no TnVed match, TnVed not exists
        _dbContext.KeyWords.AddRange(keyword1, keyword2, keyword3, keyword4, keyword5, keyword6);

        // Create KeyWordFeacnCodes
        // keyword1: single FeacnCode that matches TnVed "1111111111" -> Priority 1
        var kwfc1 = new KeyWordFeacnCode { KeyWordId = 101, FeacnCode = "1111111111", KeyWord = keyword1 };
        
        // keyword2: multiple FeacnCodes, one matches TnVed "2222222222" -> Priority 2
        var kwfc2a = new KeyWordFeacnCode { KeyWordId = 102, FeacnCode = "2222222222", KeyWord = keyword2 };
        var kwfc2b = new KeyWordFeacnCode { KeyWordId = 102, FeacnCode = "9999999999", KeyWord = keyword2 };
        
        // keyword3: single FeacnCode, doesn't match TnVed but TnVed exists in FeacnCodes -> Priority 3
        var kwfc3 = new KeyWordFeacnCode { KeyWordId = 103, FeacnCode = "8888888888", KeyWord = keyword3 };
        
        // keyword4: multiple FeacnCodes, don't match TnVed but TnVed exists in FeacnCodes -> Priority 4
        var kwfc4a = new KeyWordFeacnCode { KeyWordId = 104, FeacnCode = "7777777777", KeyWord = keyword4 };
        var kwfc4b = new KeyWordFeacnCode { KeyWordId = 104, FeacnCode = "6666666666", KeyWord = keyword4 };
        
        // keyword5: single FeacnCode, doesn't match TnVed and TnVed not in FeacnCodes -> Priority 5
        var kwfc5 = new KeyWordFeacnCode { KeyWordId = 105, FeacnCode = "5555555555", KeyWord = keyword5 };
        
        // keyword6: multiple FeacnCodes, don't match TnVed and TnVed not in FeacnCodes -> Priority 6
        var kwfc6a = new KeyWordFeacnCode { KeyWordId = 106, FeacnCode = "1010101010", KeyWord = keyword6 };
        var kwfc6b = new KeyWordFeacnCode { KeyWordId = 106, FeacnCode = "2020202020", KeyWord = keyword6 };
        
        _dbContext.KeyWordFeacnCodes.AddRange(kwfc1, kwfc2a, kwfc2b, kwfc3, kwfc4a, kwfc4b, kwfc5, kwfc6a, kwfc6b);

        // Create test parcels representing all 8 priority levels
        var parcels = new List<WbrParcel>
        {
            // Priority 1: Has keywords with exactly one distinct FeacnCode and it matches TnVed
            new WbrParcel { Id = 101, RegisterId = 1, StatusId = 1, TnVed = "1111111111" },
            
            // Priority 2: Has keywords with multiple distinct FeacnCodes and one matches TnVed
            new WbrParcel { Id = 102, RegisterId = 1, StatusId = 1, TnVed = "2222222222" },
            
            // Priority 3: Has keywords with exactly one distinct FeacnCode, doesn't match TnVed, but TnVed exists in FeacnCodes
            new WbrParcel { Id = 103, RegisterId = 1, StatusId = 1, TnVed = "3333333333" },
            
            // Priority 4: Has keywords with multiple distinct FeacnCodes, none match TnVed, but TnVed exists in FeacnCodes
            new WbrParcel { Id = 104, RegisterId = 1, StatusId = 1, TnVed = "4444444444" },
            
            // Priority 5: Has keywords with exactly one distinct FeacnCode, doesn't match TnVed, and TnVed not in FeacnCodes
            new WbrParcel { Id = 105, RegisterId = 1, StatusId = 1, TnVed = "0101010101" },
            
            // Priority 6: Has keywords with multiple distinct FeacnCodes, none match TnVed, and TnVed not in FeacnCodes
            new WbrParcel { Id = 106, RegisterId = 1, StatusId = 1, TnVed = "0202020202" },
            
            // Priority 7: No keywords but TnVed exists in FeacnCodes
            new WbrParcel { Id = 107, RegisterId = 1, StatusId = 1, TnVed = "3333333333" },
            
            // Priority 8: No keywords and TnVed not in FeacnCodes (worst match)
            new WbrParcel { Id = 108, RegisterId = 1, StatusId = 1, TnVed = "0000000000" }
        };
        _dbContext.Parcels.AddRange(parcels);

        // Create BaseParcelKeyWord relationships (priorities 1-6 have keywords, 7-8 don't)
        var bpkw1 = new BaseParcelKeyWord { BaseParcelId = 101, KeyWordId = 101, BaseParcel = parcels[0], KeyWord = keyword1 }; // Priority 1
        var bpkw2 = new BaseParcelKeyWord { BaseParcelId = 102, KeyWordId = 102, BaseParcel = parcels[1], KeyWord = keyword2 }; // Priority 2
        var bpkw3 = new BaseParcelKeyWord { BaseParcelId = 103, KeyWordId = 103, BaseParcel = parcels[2], KeyWord = keyword3 }; // Priority 3
        var bpkw4 = new BaseParcelKeyWord { BaseParcelId = 104, KeyWordId = 104, BaseParcel = parcels[3], KeyWord = keyword4 }; // Priority 4
        var bpkw5 = new BaseParcelKeyWord { BaseParcelId = 105, KeyWordId = 105, BaseParcel = parcels[4], KeyWord = keyword5 }; // Priority 5
        var bpkw6 = new BaseParcelKeyWord { BaseParcelId = 106, KeyWordId = 106, BaseParcel = parcels[5], KeyWord = keyword6 }; // Priority 6
        // Parcels 107 and 108 have no keywords (priorities 7 and 8)

        _dbContext.Set<BaseParcelKeyWord>().AddRange(bpkw1, bpkw2, bpkw3, bpkw4, bpkw5, bpkw6);
        await _dbContext.SaveChangesAsync();

        // Test ascending sort (best matches first)
        var result = await _controller.GetParcels(registerId: 1, sortBy: "feacnlookup", sortOrder: "asc");
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;

        Assert.That(pagedResult, Is.Not.Null);
        Assert.That(pagedResult!.Items.Count, Is.EqualTo(8));

        var items = pagedResult.Items.ToList();

        // Verify correct priority order (ascending = best to worst)
        Assert.That(items[0].Id, Is.EqualTo(101)); // Priority 1: Keywords with exactly one distinct FeacnCode that matches TnVed
        Assert.That(items[1].Id, Is.EqualTo(102)); // Priority 2: Keywords with multiple distinct FeacnCodes, one matches TnVed
        Assert.That(items[2].Id, Is.EqualTo(103)); // Priority 3: Keywords with exactly one distinct FeacnCode, doesn't match TnVed, TnVed exists
        Assert.That(items[3].Id, Is.EqualTo(104)); // Priority 4: Keywords with multiple distinct FeacnCodes, none match TnVed, TnVed exists
        Assert.That(items[4].Id, Is.EqualTo(105)); // Priority 5: Keywords with exactly one distinct FeacnCode, doesn't match TnVed, TnVed not exists
        Assert.That(items[5].Id, Is.EqualTo(106)); // Priority 6: Keywords with multiple distinct FeacnCodes, none match TnVed, TnVed not exists
        Assert.That(items[6].Id, Is.EqualTo(107)); // Priority 7: No keywords but TnVed exists in FeacnCodes
        Assert.That(items[7].Id, Is.EqualTo(108)); // Priority 8: No keywords and TnVed not in FeacnCodes
    }

    [Test]
    public async Task GetParcels_SortsByMatch_Descending_WithCorrectEightPriorities()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 2, CompanyId = 1, FileName = "r.xlsx" }; // Ozon register
        _dbContext.Registers.Add(register);

        // Create test FeacnCodes
        var feacnCode1 = new FeacnCode { Id = 10, Code = "1111111111", CodeEx = "", Name = "", NormalizedName = "" };
        var feacnCode2 = new FeacnCode { Id = 11, Code = "2222222222", CodeEx = "", Name = "", NormalizedName = "" };
        _dbContext.FeacnCodes.AddRange(feacnCode1, feacnCode2);

        // Create Keywords
        var keyword1 = new KeyWord { Id = 201, Word = "diamond", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }; // Single FeacnCode
        var keyword2 = new KeyWord { Id = 202, Word = "platinum", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols }; // Multiple FeacnCodes
        _dbContext.KeyWords.AddRange(keyword1, keyword2);

        // Create KeyWordFeacnCodes
        var kwfc1 = new KeyWordFeacnCode { KeyWordId = 201, FeacnCode = "1111111111", KeyWord = keyword1 }; // Single
        var kwfc2a = new KeyWordFeacnCode { KeyWordId = 202, FeacnCode = "2222222222", KeyWord = keyword2 }; // Multiple
        var kwfc2b = new KeyWordFeacnCode { KeyWordId = 202, FeacnCode = "3333333333", KeyWord = keyword2 };
        _dbContext.KeyWordFeacnCodes.AddRange(kwfc1, kwfc2a, kwfc2b);

        // Create test parcels for different priorities
        var parcels = new List<OzonParcel>
        {
            // Priority 1: Has keywords with exactly one matching FeacnCode for TnVed (best match)
            new OzonParcel { Id = 201, RegisterId = 2, StatusId = 1, TnVed = "1111111111" },
            
            // Priority 2: Has keywords with multiple matching FeacnCodes for TnVed
            new OzonParcel { Id = 202, RegisterId = 2, StatusId = 1, TnVed = "2222222222" },
            
            // Priority 6: No keywords and TnVed not in FeacnCodes (worst match)
            new OzonParcel { Id = 206, RegisterId = 2, StatusId = 1, TnVed = "9999999999" }
        };
        _dbContext.Parcels.AddRange(parcels);

        // Create BaseParcelKeyWord relationships
        var bpkw1 = new BaseParcelKeyWord { BaseParcelId = 201, KeyWordId = 201, BaseParcel = parcels[0], KeyWord = keyword1 };
        var bpkw2 = new BaseParcelKeyWord { BaseParcelId = 202, KeyWordId = 202, BaseParcel = parcels[1], KeyWord = keyword2 };
        _dbContext.Set<BaseParcelKeyWord>().AddRange(bpkw1, bpkw2);
        await _dbContext.SaveChangesAsync();

        // Test descending sort (worst matches first)
        var result = await _controller.GetParcels(registerId: 2, sortBy: "feacnlookup", sortOrder: "desc");
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;

        Assert.That(pagedResult, Is.Not.Null);
        Assert.That(pagedResult!.Items.Count, Is.EqualTo(3));

        var items = pagedResult.Items.ToList();

        // Verify correct priority order (descending = worst to best)
        Assert.That(items[0].Id, Is.EqualTo(206)); // Priority 6: No keywords and TnVed not in FeacnCodes
        Assert.That(items[1].Id, Is.EqualTo(202)); // Priority 2: Keywords with multiple matching FeacnCodes
        Assert.That(items[2].Id, Is.EqualTo(201)); // Priority 1: Keywords with exactly one matching FeacnCode
    }

    [Test]
    public async Task GetParcels_SortsByMatch_CorrectlyDistinguishesSingleVsMultipleFeacnCodes()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 3, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(register);

        // Create Keywords with different FeacnCode counts
        var singleFeacnKeyword = new KeyWord { Id = 301, Word = "unique", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        var multipleFeacnKeyword = new KeyWord { Id = 302, Word = "common", MatchTypeId = (int)WordMatchTypeCode.ExactSymbols };
        _dbContext.KeyWords.AddRange(singleFeacnKeyword, multipleFeacnKeyword);

        // Single FeacnCode for first keyword
        var kwfc1 = new KeyWordFeacnCode { KeyWordId = 301, FeacnCode = "1111111111", KeyWord = singleFeacnKeyword };
        
        // Multiple FeacnCodes for second keyword
        var kwfc2a = new KeyWordFeacnCode { KeyWordId = 302, FeacnCode = "2222222222", KeyWord = multipleFeacnKeyword };
        var kwfc2b = new KeyWordFeacnCode { KeyWordId = 302, FeacnCode = "3333333333", KeyWord = multipleFeacnKeyword };
        var kwfc2c = new KeyWordFeacnCode { KeyWordId = 302, FeacnCode = "4444444444", KeyWord = multipleFeacnKeyword };
        
        _dbContext.KeyWordFeacnCodes.AddRange(kwfc1, kwfc2a, kwfc2b, kwfc2c);

        // Create parcels with non-matching TnVeds (to test priority 3 vs 4)
        var parcels = new List<WbrParcel>
        {
            // Should be Priority 3: Has keywords but exactly one FeacnCode (non-matching)
            new WbrParcel { Id = 301, RegisterId = 3, StatusId = 1, TnVed = "9999999999" },
            
            // Should be Priority 4: Has keywords but multiple FeacnCodes (non-matching)
            new WbrParcel { Id = 302, RegisterId = 3, StatusId = 1, TnVed = "8888888888" }
        };
        _dbContext.Parcels.AddRange(parcels);

        // Create BaseParcelKeyWord relationships
        var bpkw1 = new BaseParcelKeyWord { BaseParcelId = 301, KeyWordId = 301, BaseParcel = parcels[0], KeyWord = singleFeacnKeyword };
        var bpkw2 = new BaseParcelKeyWord { BaseParcelId = 302, KeyWordId = 302, BaseParcel = parcels[1], KeyWord = multipleFeacnKeyword };
        _dbContext.Set<BaseParcelKeyWord>().AddRange(bpkw1, bpkw2);
        await _dbContext.SaveChangesAsync();

        // Test ascending sort
        var result = await _controller.GetParcels(registerId: 3, sortBy: "feacnlookup", sortOrder: "asc");
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;

        Assert.That(pagedResult, Is.Not.Null);
        Assert.That(pagedResult!.Items.Count, Is.EqualTo(2));

        var items = pagedResult.Items.ToList();

        // Priority 3 should come before Priority 4
        Assert.That(items[0].Id, Is.EqualTo(301)); // Priority 3: Single FeacnCode
        Assert.That(items[1].Id, Is.EqualTo(302)); // Priority 4: Multiple FeacnCodes
    }

    [Test]
    public async Task GetParcels_ValidatesFeacnLookupSortBy_ForWBR()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, CompanyId = 2, FileName = "r.xlsx" }; // WBR
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetParcels(registerId: 1, sortBy: "feacnlookup", sortOrder: "asc");

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        Assert.That(pagedResult!.Sorting.SortBy, Is.EqualTo("feacnlookup"));
        Assert.That(pagedResult.Sorting.SortOrder, Is.EqualTo("asc"));
    }

    [Test]
    public async Task GetParcels_ValidatesFeacnLookupSortBy_ForOzon()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 2, CompanyId = 1, FileName = "r.xlsx" }; // Ozon
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetParcels(registerId: 2, sortBy: "feacnlookup", sortOrder: "desc");

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;
        Assert.That(pagedResult!.Sorting.SortBy, Is.EqualTo("feacnlookup"));
        Assert.That(pagedResult.Sorting.SortOrder, Is.EqualTo("desc"));
    }

    [Test]
    public async Task GetParcels_FeacnLookupSort_WorksWithPagination()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 3, CompanyId = 2, FileName = "r.xlsx" };
        _dbContext.Registers.Add(register);

        // Create multiple parcels to test pagination
        for (int i = 1; i <= 5; i++)
        {
            _dbContext.Parcels.Add(new WbrParcel
            {
                Id = 300 + i,
                RegisterId = 3,
                StatusId = 1,
                TnVed = $"{i}{i}{i}{i}{i}{i}{i}{i}{i}{i}"
            });
        }
        await _dbContext.SaveChangesAsync();

        // Test with pagination
        var result = await _controller.GetParcels(
            registerId: 3,
            sortBy: "feacnlookup",
            sortOrder: "asc",
            page: 1,
            pageSize: 3);

        var okResult = result.Result as OkObjectResult;
        var pagedResult = okResult!.Value as PagedResult<ParcelViewItem>;

        Assert.That(pagedResult, Is.Not.Null);
        Assert.That(pagedResult!.Items.Count, Is.EqualTo(3));
        Assert.That(pagedResult.Pagination.CurrentPage, Is.EqualTo(1));
        Assert.That(pagedResult.Pagination.PageSize, Is.EqualTo(3));
        Assert.That(pagedResult.Pagination.TotalCount, Is.EqualTo(5));
        Assert.That(pagedResult.Sorting.SortBy, Is.EqualTo("feacnlookup"));
    }
    #endregion
}