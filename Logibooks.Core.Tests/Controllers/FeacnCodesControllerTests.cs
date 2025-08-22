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
    public async Task Upload_ReturnsNoContent_ForExcelFile()
    {
        SetCurrentUserId(1);
        byte[] content = [1, 2, 3];
        var mockFile = CreateMockFile("codes.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", content);
        _mockProcessingService
            .Setup(s => s.UploadFeacnCodesAsync(It.Is<byte[]>(b => b.SequenceEqual(content)), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Upload(mockFile.Object);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Upload_ReturnsNoContent_ForZipFile()
    {
        SetCurrentUserId(1);
        byte[] zipContent = File.ReadAllBytes(Path.Combine(testDataDir, "Реестр_207730349.zip"));
        var mockFile = CreateMockFile("Реестр_207730349.zip", "application/zip", zipContent);
        _mockProcessingService.Setup(s => s.UploadFeacnCodesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Upload(mockFile.Object);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Upload_ReturnsBadRequest_ForUnsupportedFile()
    {
        SetCurrentUserId(1);
        var mockFile = CreateMockFile("file.txt", "text/plain", [1, 2, 3]);

        var result = await _controller.Upload(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        _mockProcessingService.Verify(s => s.UploadFeacnCodesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Upload_ReturnsBadRequest_WhenZipWithoutExcel()
    {
        SetCurrentUserId(1);
        byte[] zipContent = File.ReadAllBytes(Path.Combine(testDataDir, "Zip_Empty.zip"));
        var mockFile = CreateMockFile("Zip_Empty.zip", "application/zip", zipContent);

        var result = await _controller.Upload(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        _mockProcessingService.Verify(s => s.UploadFeacnCodesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Upload_ReturnsBadRequest_WhenFileEmpty()
    {
        SetCurrentUserId(1);
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        var result = await _controller.Upload(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        _mockProcessingService.Verify(s => s.UploadFeacnCodesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
