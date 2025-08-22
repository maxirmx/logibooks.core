using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class FeacnCodesControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<FeacnCodesController> _logger;
    private FeacnCodesController _controller;
    private Role _userRole;
    private User _user;
    private Mock<IFeacnListProcessingService> _mockProcessingService;
#pragma warning restore CS8618

    private readonly string testDataDir = Path.Combine(AppContext.BaseDirectory, "test.data");

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"feacncodes_controller_db_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _userRole = new Role { Id = 1, Name = "user", Title = "User" };
        _dbContext.Roles.Add(_userRole);
        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _user = new User
        {
            Id = 1,
            Email = "user@example.com",
            Password = hpw,
            UserRoles = [ new UserRole { UserId = 1, RoleId = 1, Role = _userRole } ]
        };
        _dbContext.Users.Add(_user);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<FeacnCodesController>();
        _mockProcessingService = new Mock<IFeacnListProcessingService>();
        _controller = new FeacnCodesController(_mockHttpContextAccessor.Object, _dbContext, _logger, _mockProcessingService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private void SetCurrentUserId(int id)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = id;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(ctx);
        _controller = new FeacnCodesController(_mockHttpContextAccessor.Object, _dbContext, _logger, _mockProcessingService.Object)
        {
            ControllerContext = { HttpContext = ctx }
        };
    }

    [Test]
    public async Task Get_ReturnsDto_WhenExists()
    {
        SetCurrentUserId(1);
        var code = new FeacnCode { Id = 10, Code = "1234567890", CodeEx = "1234567890", Name = "Name", NormalizedName = "NAME" };
        _dbContext.FeacnCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Get(10);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(10));
    }

    [Test]
    public async Task Get_ReturnsDto_WhenExistsAndFromDateIsNull()
    {
        SetCurrentUserId(1);
        var code = new FeacnCode 
        { 
            Id = 11, 
            Code = "1234567891", 
            CodeEx = "1234567891", 
            Name = "Name", 
            NormalizedName = "NAME",
            FromDate = null
        };
        _dbContext.FeacnCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Get(11);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(11));
    }

    [Test]
    public async Task Get_ReturnsDto_WhenExistsAndFromDateIsInPast()
    {
        SetCurrentUserId(1);
        var code = new FeacnCode 
        { 
            Id = 12, 
            Code = "1234567892", 
            CodeEx = "1234567892", 
            Name = "Name", 
            NormalizedName = "NAME",
            FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5))
        };
        _dbContext.FeacnCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Get(12);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(12));
    }

    [Test]
    public async Task Get_ReturnsNotFound_WhenFromDateIsInFuture()
    {
        SetCurrentUserId(1);
        var code = new FeacnCode 
        { 
            Id = 13, 
            Code = "1234567893", 
            CodeEx = "1234567893", 
            Name = "Name", 
            NormalizedName = "NAME",
            FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))
        };
        _dbContext.FeacnCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Get(13);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.Get(999);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetByCode_ReturnsBadRequest_OnInvalidCode()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetByCode("123");
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetByCode_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetByCode("1234567890");
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetByCode_ReturnsDto_WhenExists()
    {
        SetCurrentUserId(1);
        var code = new FeacnCode { Id = 20, Code = "1234567890", CodeEx = "1234567890", Name = "N1", NormalizedName = "N1" };
        _dbContext.FeacnCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetByCode("1234567890");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Code, Is.EqualTo("1234567890"));
    }

    [Test]
    public async Task GetByCode_ReturnsDto_WhenExistsAndFromDateIsNull()
    {
        SetCurrentUserId(1);
        var code = new FeacnCode 
        { 
            Id = 21, 
            Code = "1234567821", 
            CodeEx = "1234567821", 
            Name = "N1", 
            NormalizedName = "N1",
            FromDate = null
        };
        _dbContext.FeacnCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetByCode("1234567821");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Code, Is.EqualTo("1234567821"));
    }

    [Test]
    public async Task GetByCode_ReturnsDto_WhenExistsAndFromDateIsInPast()
    {
        SetCurrentUserId(1);
        var code = new FeacnCode 
        { 
            Id = 22, 
            Code = "1234567822", 
            CodeEx = "1234567822", 
            Name = "N1", 
            NormalizedName = "N1",
            FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3))
        };
        _dbContext.FeacnCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetByCode("1234567822");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Code, Is.EqualTo("1234567822"));
    }

    [Test]
    public async Task GetByCode_ReturnsNotFound_WhenFromDateIsInFuture()
    {
        SetCurrentUserId(1);
        var code = new FeacnCode 
        { 
            Id = 23, 
            Code = "1234567823", 
            CodeEx = "1234567823", 
            Name = "N1", 
            NormalizedName = "N1",
            FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))
        };
        _dbContext.FeacnCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetByCode("1234567823");

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Lookup_ReturnsMatchingCodes()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "A", NormalizedName = "ABC", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "B", NormalizedName = "XYZ", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 3, Code = "3333333333", CodeEx = "3333333333", Name = "C", NormalizedName = "ABC", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Lookup("abc");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        Assert.That(result.Value!.First().Code, Is.EqualTo("1111111111"));
    }

    [Test]
    public async Task Lookup_ReturnsCodesByPrefix_WhenKeyIsDigitsOnly()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1234567890", CodeEx = "1234567890", Name = "Product A", NormalizedName = "PRODUCT A", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 2, Code = "1235678901", CodeEx = "1235678901", Name = "Product B", NormalizedName = "PRODUCT B", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 3, Code = "9876543210", CodeEx = "9876543210", Name = "Product C", NormalizedName = "PRODUCT C", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 4, Code = "1230000000", CodeEx = "1230000000", Name = "Product D", NormalizedName = "PRODUCT D", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) } // Future date - should be filtered out
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Lookup("123");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2)); // Only codes starting with "123" and with valid dates
        var codes = result.Value!.ToList();
        Assert.That(codes.Any(c => c.Code == "1234567890"), Is.True, "Should include code starting with 123");
        Assert.That(codes.Any(c => c.Code == "1235678901"), Is.True, "Should include code starting with 123");
        Assert.That(codes.Any(c => c.Code == "9876543210"), Is.False, "Should not include code not starting with 123");
        Assert.That(codes.Any(c => c.Code == "1230000000"), Is.False, "Should not include code with future FromDate");
    }

    [Test]
    public async Task Lookup_ReturnsCodesByNormalizedName_WhenKeyContainsNonDigits()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "Metal Product", NormalizedName = "METAL PRODUCT", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "Wood Product", NormalizedName = "WOOD PRODUCT", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 3, Code = "3333333333", CodeEx = "3333333333", Name = "Metal Tools", NormalizedName = "METAL TOOLS", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Lookup("metal");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2)); // Both codes containing "METAL" in normalized name
        var codes = result.Value!.ToList();
        Assert.That(codes.Any(c => c.Code == "1111111111"), Is.True, "Should include Metal Product");
        Assert.That(codes.Any(c => c.Code == "3333333333"), Is.True, "Should include Metal Tools");
        Assert.That(codes.Any(c => c.Code == "2222222222"), Is.False, "Should not include Wood Product");
    }

    [Test]
    public async Task Lookup_ReturnsCodesByNormalizedName_WhenKeyContainsMixedCharacters()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "ABC123 Product", NormalizedName = "ABC123 PRODUCT", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 2, Code = "1234567890", CodeEx = "1234567890", Name = "XYZ789 Item", NormalizedName = "XYZ789 ITEM", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }
        );
        await _dbContext.SaveChangesAsync();

        // Mixed characters should search by normalized name, not code prefix
        var result = await _controller.Lookup("abc123");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        Assert.That(result.Value!.First().Code, Is.EqualTo("1111111111"));
    }

    [Test]
    public async Task Lookup_ReturnsEmptyList_WhenNoDigitsOnlyMatchFound()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "Product A", NormalizedName = "PRODUCT A", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "Product B", NormalizedName = "PRODUCT B", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }
        );
        await _dbContext.SaveChangesAsync();

        // Search for digits that don't match any code prefix
        var result = await _controller.Lookup("999");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Lookup_ReturnsEmptyList_WhenNoTextMatchFound()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "Metal Product", NormalizedName = "METAL PRODUCT", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "Wood Product", NormalizedName = "WOOD PRODUCT", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }
        );
        await _dbContext.SaveChangesAsync();

        // Search for text that doesn't match any normalized name
        var result = await _controller.Lookup("plastic");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Lookup_HandlesSingleDigit_CorrectlyAsDigitsOnly()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1234567890", CodeEx = "1234567890", Name = "Product A", NormalizedName = "PRODUCT A", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 2, Code = "9876543210", CodeEx = "9876543210", Name = "Product B", NormalizedName = "PRODUCT B", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }
        );
        await _dbContext.SaveChangesAsync();

        // Single digit should be treated as digits-only search
        var result = await _controller.Lookup("1");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        Assert.That(result.Value!.First().Code, Is.EqualTo("1234567890"));
    }

    [Test]
    public async Task Lookup_HandlesEmptyString_GracefullyAsText()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "Product A", NormalizedName = "PRODUCT A", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }
        );
        await _dbContext.SaveChangesAsync();

        // Empty string should return no results due to explicit handling
        var result = await _controller.Lookup("");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(0), "Empty string should return no results");
    }

    [Test]
    public async Task Lookup_HandlesWhitespaceString_GracefullyAsText()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "Product A", NormalizedName = "PRODUCT A", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }
        );
        await _dbContext.SaveChangesAsync();

        // Whitespace-only string should return no results due to explicit handling
        var result = await _controller.Lookup("   ");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(0), "Whitespace-only string should return no results");
    }

    [Test]
    public async Task Lookup_IsCaseInsensitive_ForTextSearch()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "Metal Product", NormalizedName = "METAL PRODUCT", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }
        );
        await _dbContext.SaveChangesAsync();

        // Test different case variations
        var resultLower = await _controller.Lookup("metal");
        var resultUpper = await _controller.Lookup("METAL");
        var resultMixed = await _controller.Lookup("Metal");

        Assert.That(resultLower.Value!.Count(), Is.EqualTo(1));
        Assert.That(resultUpper.Value!.Count(), Is.EqualTo(1));
        Assert.That(resultMixed.Value!.Count(), Is.EqualTo(1));
        
        // All should return the same result
        Assert.That(resultLower.Value!.First().Code, Is.EqualTo("1111111111"));
        Assert.That(resultUpper.Value!.First().Code, Is.EqualTo("1111111111"));
        Assert.That(resultMixed.Value!.First().Code, Is.EqualTo("1111111111"));
    }

    [Test]
    public async Task Lookup_ReturnsResultsOrderedById()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 3, Code = "1230000003", CodeEx = "1230000003", Name = "Product C", NormalizedName = "PRODUCT C", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 1, Code = "1230000001", CodeEx = "1230000001", Name = "Product A", NormalizedName = "PRODUCT A", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 2, Code = "1230000002", CodeEx = "1230000002", Name = "Product B", NormalizedName = "PRODUCT B", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }
        );
        await _dbContext.SaveChangesAsync();

        // Search by prefix should return results ordered by ID
        var result = await _controller.Lookup("123");

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(3));
        
        var codes = result.Value!.ToList();
        Assert.That(codes[0].Id, Is.EqualTo(1), "First result should have ID 1");
        Assert.That(codes[1].Id, Is.EqualTo(2), "Second result should have ID 2");
        Assert.That(codes[2].Id, Is.EqualTo(3), "Third result should have ID 3");
    }

    [Test]
    public async Task Lookup_HandlesSpecialCharacters_InTextSearch()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "Special-Product", NormalizedName = "SPECIAL-PRODUCT", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "Product & Item", NormalizedName = "PRODUCT & ITEM", FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }
        );
        await _dbContext.SaveChangesAsync();

        // Test search with special characters
        var resultHyphen = await _controller.Lookup("special-");
        var resultAmpersand = await _controller.Lookup(" & ");

        Assert.That(resultHyphen.Value!.Count(), Is.EqualTo(1));
        Assert.That(resultHyphen.Value!.First().Code, Is.EqualTo("1111111111"));
        
        Assert.That(resultAmpersand.Value!.Count(), Is.EqualTo(1));
        Assert.That(resultAmpersand.Value!.First().Code, Is.EqualTo("2222222222"));
    }

    [Test]
    public async Task Children_ReturnsEmpty_WhenParentHasNoChildren()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "Standalone", NormalizedName = "STANDALONE" },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "Another", NormalizedName = "ANOTHER" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Children_ReturnsEmpty_WhenParentDoesNotExist()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "Code1", NormalizedName = "CODE1" },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "Code2", NormalizedName = "CODE2" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(999); // Non-existent parent

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Children_ReturnsMultipleLevels_ButOnlyDirectChildren()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "Root", NormalizedName = "ROOT" },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "Child1", NormalizedName = "CHILD1", ParentId = 1 },
            new FeacnCode { Id = 3, Code = "3333333333", CodeEx = "3333333333", Name = "Child2", NormalizedName = "CHILD2", ParentId = 1 },
            new FeacnCode { Id = 4, Code = "4444444444", CodeEx = "4444444444", Name = "Grandchild1", NormalizedName = "GRANDCHILD1", ParentId = 2 },
            new FeacnCode { Id = 5, Code = "5555555555", CodeEx = "5555555555", Name = "Grandchild2", NormalizedName = "GRANDCHILD2", ParentId = 2 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2)); // Only direct children, not grandchildren
        var children = result.Value!.ToList();
        Assert.That(children.Any(c => c.Id == 2), Is.True, "Should include direct child");
        Assert.That(children.Any(c => c.Id == 3), Is.True, "Should include direct child");
        Assert.That(children.Any(c => c.Id == 4), Is.False, "Should not include grandchild");
        Assert.That(children.Any(c => c.Id == 5), Is.False, "Should not include grandchild");
    }

    [Test]
    public async Task Children_FiltersOutFutureDatedCodes_ForSpecificParent()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode 
            { 
                Id = 10, 
                Code = "1000000010", 
                CodeEx = "1000000010", 
                Name = "Parent", 
                NormalizedName = "PARENT",
                FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
            },
            new FeacnCode 
            { 
                Id = 11, 
                Code = "1000000011", 
                CodeEx = "1000000011", 
                Name = "ValidChild", 
                NormalizedName = "VALIDCHILD", 
                ParentId = 10,
                FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
            },
            new FeacnCode 
            { 
                Id = 12, 
                Code = "1000000012", 
                CodeEx = "1000000012", 
                Name = "FutureChild", 
                NormalizedName = "FUTURECHILD", 
                ParentId = 10,
                FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)) // Future date
            },
            new FeacnCode 
            { 
                Id = 13, 
                Code = "1000000013", 
                CodeEx = "1000000013", 
                Name = "NullDateChild", 
                NormalizedName = "NULLDATECHILD", 
                ParentId = 10,
                FromDate = null
            }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(10);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2)); // Should return valid child and null date child
        var children = result.Value!.ToList();
        Assert.That(children.Any(c => c.Id == 11), Is.True, "Should include child with past FromDate");
        Assert.That(children.Any(c => c.Id == 12), Is.False, "Should exclude child with future FromDate");
        Assert.That(children.Any(c => c.Id == 13), Is.True, "Should include child with null FromDate");
    }

    [Test]
    public async Task Children_OrdersResultsById()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 10, Code = "1000000010", CodeEx = "1000000010", Name = "Parent", NormalizedName = "PARENT" },
            new FeacnCode { Id = 13, Code = "1000000013", CodeEx = "1000000013", Name = "Child3", NormalizedName = "CHILD3", ParentId = 10 },
            new FeacnCode { Id = 11, Code = "1000000011", CodeEx = "1000000011", Name = "Child1", NormalizedName = "CHILD1", ParentId = 10 },
            new FeacnCode { Id = 15, Code = "1000000015", CodeEx = "1000000015", Name = "Child5", NormalizedName = "CHILD5", ParentId = 10 },
            new FeacnCode { Id = 12, Code = "1000000012", CodeEx = "1000000012", Name = "Child2", NormalizedName = "CHILD2", ParentId = 10 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(10);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(4));
        
        var children = result.Value!.ToList();
        Assert.That(children[0].Id, Is.EqualTo(11), "First result should have ID 11");
        Assert.That(children[1].Id, Is.EqualTo(12), "Second result should have ID 12");
        Assert.That(children[2].Id, Is.EqualTo(13), "Third result should have ID 13");
        Assert.That(children[3].Id, Is.EqualTo(15), "Fourth result should have ID 15");
    }

    [Test]
    public async Task Children_WorksWithComplexHierarchy()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            // Level 0 (roots)
            new FeacnCode { Id = 1, Code = "1000000001", CodeEx = "1000000001", Name = "Root1", NormalizedName = "ROOT1" },
            new FeacnCode { Id = 2, Code = "1000000002", CodeEx = "1000000002", Name = "Root2", NormalizedName = "ROOT2" },
            
            // Level 1 (children of root1)
            new FeacnCode { Id = 11, Code = "1000000011", CodeEx = "1000000011", Name = "Child1-1", NormalizedName = "CHILD1-1", ParentId = 1 },
            new FeacnCode { Id = 12, Code = "1000000012", CodeEx = "1000000012", Name = "Child1-2", NormalizedName = "CHILD1-2", ParentId = 1 },
            
            // Level 1 (children of root2)
            new FeacnCode { Id = 21, Code = "1000000021", CodeEx = "1000000021", Name = "Child2-1", NormalizedName = "CHILD2-1", ParentId = 2 },
            
            // Level 2 (grandchildren)
            new FeacnCode { Id = 111, Code = "1000000111", CodeEx = "1000000111", Name = "Grandchild1-1-1", NormalizedName = "GRANDCHILD1-1-1", ParentId = 11 },
            new FeacnCode { Id = 112, Code = "1000000112", CodeEx = "1000000112", Name = "Grandchild1-1-2", NormalizedName = "GRANDCHILD1-1-2", ParentId = 11 }
        );
        await _dbContext.SaveChangesAsync();

        // Test getting children of root1
        var root1Children = await _controller.Children(1);
        Assert.That(root1Children.Value!.Count(), Is.EqualTo(2));
        Assert.That(root1Children.Value!.Any(c => c.Id == 11), Is.True);
        Assert.That(root1Children.Value!.Any(c => c.Id == 12), Is.True);

        // Test getting children of root2
        var root2Children = await _controller.Children(2);
        Assert.That(root2Children.Value!.Count(), Is.EqualTo(1));
        Assert.That(root2Children.Value!.Any(c => c.Id == 21), Is.True);

        // Test getting grandchildren
        var grandchildren = await _controller.Children(11);
        Assert.That(grandchildren.Value!.Count(), Is.EqualTo(2));
        Assert.That(grandchildren.Value!.Any(c => c.Id == 111), Is.True);
        Assert.That(grandchildren.Value!.Any(c => c.Id == 112), Is.True);
    }

    [Test]
    public async Task Children_ReturnsCorrectDtoStructure()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode 
            { 
                Id = 1, 
                Code = "1234567890", 
                CodeEx = "1234567899", 
                Name = "Parent Code", 
                NormalizedName = "PARENT CODE",
                FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
                ToDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
                OldName = "Old Parent Name",
                OldNameToDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5))
            },
            new FeacnCode 
            { 
                Id = 2, 
                Code = "9876543210", 
                CodeEx = "9876543219", 
                Name = "Child Code", 
                NormalizedName = "CHILD CODE",
                ParentId = 1,
                FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
                ToDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
                OldName = "Old Child Name",
                OldNameToDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2))
            }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        
        var childDto = result.Value!.First();
        Assert.That(childDto.Id, Is.EqualTo(2));
        Assert.That(childDto.Code, Is.EqualTo("9876543210"));
        Assert.That(childDto.CodeEx, Is.EqualTo("9876543219"));
        Assert.That(childDto.Name, Is.EqualTo("Child Code"));
        Assert.That(childDto.NormalizedName, Is.EqualTo("CHILD CODE"));
        Assert.That(childDto.ParentId, Is.EqualTo(1));
    }

    [Test]
    public async Task Children_WithNullId_ReturnsOnlyTopLevelNodes()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            // Top level nodes (no parent)
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "TopLevel1", NormalizedName = "TOPLEVEL1" },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "TopLevel2", NormalizedName = "TOPLEVEL2" },
            
            // Child nodes (have parents)
            new FeacnCode { Id = 3, Code = "3333333333", CodeEx = "3333333333", Name = "Child1", NormalizedName = "CHILD1", ParentId = 1 },
            new FeacnCode { Id = 4, Code = "4444444444", CodeEx = "4444444444", Name = "Child2", NormalizedName = "CHILD2", ParentId = 2 },
            
            // Top level with future date (should be filtered out)
            new FeacnCode 
            { 
                Id = 5, 
                Code = "5555555555", 
                CodeEx = "5555555555", 
                Name = "FutureTopLevel", 
                NormalizedName = "FUTURETOPLEVEL",
                FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))
            }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(null);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2)); // Only valid top-level nodes
        var topLevelNodes = result.Value!.ToList();
        Assert.That(topLevelNodes.Any(c => c.Id == 1), Is.True, "Should include first top-level node");
        Assert.That(topLevelNodes.Any(c => c.Id == 2), Is.True, "Should include second top-level node");
        Assert.That(topLevelNodes.Any(c => c.Id == 3), Is.False, "Should not include child node");
        Assert.That(topLevelNodes.Any(c => c.Id == 4), Is.False, "Should not include child node");
        Assert.That(topLevelNodes.Any(c => c.Id == 5), Is.False, "Should not include future-dated top-level node");
    }

    [Test]
    public async Task Children_HandlesLargeNumberOfChildren()
    {
        SetCurrentUserId(1);
        var codes = new System.Collections.Generic.List<FeacnCode>
        {
            // Parent
            new FeacnCode { Id = 1, Code = "1000000001", CodeEx = "1000000001", Name = "Parent", NormalizedName = "PARENT" }
        };

        // Add 100 children
        for (int i = 2; i <= 101; i++)
        {
            codes.Add(new FeacnCode 
            { 
                Id = i, 
                Code = $"100000{i:D4}", 
                CodeEx = $"100000{i:D4}", 
                Name = $"Child{i}", 
                NormalizedName = $"CHILD{i}", 
                ParentId = 1 
            });
        }

        _dbContext.FeacnCodes.AddRange(codes);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(100));
        
        // Verify ordering (should be ordered by Id)
        var children = result.Value!.ToList();
        for (int i = 0; i < children.Count - 1; i++)
        {
            Assert.That(children[i].Id, Is.LessThan(children[i + 1].Id), 
                $"Children should be ordered by Id: {children[i].Id} should be less than {children[i + 1].Id}");
        }
    }

    private static Mock<IFormFile> CreateMockFile(string fileName, string contentType, byte[] content)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((stream, token) => { stream.Write(content, 0, content.Length); })
            .Returns(Task.CompletedTask);
        return mockFile;
    }
}
