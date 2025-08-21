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
    public async Task Children_ReturnsTopLevel_WhenIdNull()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "root", NormalizedName = "ROOT" },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "child", NormalizedName = "CHILD", ParentId = 1 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(null);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        Assert.That(result.Value!.First().Id, Is.EqualTo(1));
    }

    [Test]
    public async Task Children_ReturnsChildren_ForGivenId()
    {
        SetCurrentUserId(1);
        _dbContext.FeacnCodes.AddRange(
            new FeacnCode { Id = 1, Code = "1111111111", CodeEx = "1111111111", Name = "root", NormalizedName = "ROOT" },
            new FeacnCode { Id = 2, Code = "2222222222", CodeEx = "2222222222", Name = "child1", NormalizedName = "CHILD1", ParentId = 1 },
            new FeacnCode { Id = 3, Code = "3333333333", CodeEx = "3333333333", Name = "child2", NormalizedName = "CHILD2", ParentId = 1 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Children(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Upload_ReturnsGuid_ForExcelFile()
    {
        SetCurrentUserId(1);
        byte[] content = [1, 2, 3];
        var mockFile = CreateMockFile("codes.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", content);
        var handle = Guid.NewGuid();
        _mockProcessingService
            .Setup(s => s.StartProcessingAsync(It.Is<byte[]>(b => b.SequenceEqual(content)), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handle);

        var result = await _controller.Upload(mockFile.Object);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(((GuidReference)ok!.Value!).Id, Is.EqualTo(handle));
    }

    [Test]
    public async Task Upload_ReturnsGuid_ForZipFile()
    {
        SetCurrentUserId(1);
        byte[] zipContent = File.ReadAllBytes(Path.Combine(testDataDir, "Реестр_207730349.zip"));
        var mockFile = CreateMockFile("Реестр_207730349.zip", "application/zip", zipContent);
        var handle = Guid.NewGuid();
        _mockProcessingService.Setup(s => s.StartProcessingAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(handle);

        var result = await _controller.Upload(mockFile.Object);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(((GuidReference)ok!.Value!).Id, Is.EqualTo(handle));
    }

    [Test]
    public async Task Upload_ReturnsBadRequest_ForUnsupportedFile()
    {
        SetCurrentUserId(1);
        var mockFile = CreateMockFile("file.txt", "text/plain", [1, 2, 3]);

        var result = await _controller.Upload(mockFile.Object);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        _mockProcessingService.Verify(s => s.StartProcessingAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Upload_ReturnsBadRequest_WhenZipWithoutExcel()
    {
        SetCurrentUserId(1);
        byte[] zipContent = File.ReadAllBytes(Path.Combine(testDataDir, "Zip_Empty.zip"));
        var mockFile = CreateMockFile("Zip_Empty.zip", "application/zip", zipContent);

        var result = await _controller.Upload(mockFile.Object);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        _mockProcessingService.Verify(s => s.StartProcessingAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Upload_ReturnsBadRequest_WhenFileEmpty()
    {
        SetCurrentUserId(1);
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        var result = await _controller.Upload(mockFile.Object);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        _mockProcessingService.Verify(s => s.StartProcessingAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetUploadProgress_ReturnsData()
    {
        SetCurrentUserId(1);
        var handle = Guid.NewGuid();
        var progress = new ValidationProgress { HandleId = handle, Total = 10, Processed = 5 };
        _mockProcessingService.Setup(s => s.GetProgress(handle)).Returns(progress);

        var result = await _controller.GetUploadProgress(handle);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.EqualTo(progress));
    }

    [Test]
    public async Task GetUploadProgress_ReturnsNotFound()
    {
        SetCurrentUserId(1);
        var handle = Guid.NewGuid();
        _mockProcessingService.Setup(s => s.GetProgress(handle)).Returns((ValidationProgress?)null);

        var result = await _controller.GetUploadProgress(handle);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task CancelUpload_ReturnsNoContent()
    {
        SetCurrentUserId(1);
        var handle = Guid.NewGuid();
        _mockProcessingService.Setup(s => s.Cancel(handle)).Returns(true);

        var result = await _controller.CancelUpload(handle);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task CancelUpload_ReturnsNotFound()
    {
        SetCurrentUserId(1);
        var handle = Guid.NewGuid();
        _mockProcessingService.Setup(s => s.Cancel(handle)).Returns(false);

        var result = await _controller.CancelUpload(handle);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
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
