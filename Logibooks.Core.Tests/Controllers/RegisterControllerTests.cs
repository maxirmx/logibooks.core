using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using NUnit.Framework;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using System.IO;
using System.Threading;
using System;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class RegisterControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<RegisterController> _logger;
    private RegisterController _controller;
    private Role _logistRole;
    private Role _adminRole;
    private User _logistUser;
    private User _adminUser;
    private string testDataDir = Path.Combine(AppContext.BaseDirectory, "test.data");
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"register_controller_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _logistRole = new Role { Id = 1, Name = "logist", Title = "Логист" };
        _adminRole = new Role { Id = 2, Name = "administrator", Title = "Администратор" };
        _dbContext.Roles.AddRange(_logistRole, _adminRole);

        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _logistUser = new User
        {
            Id = 1,
            Email = "logist@example.com",
            Password = hpw,
            FirstName = "Log",
            LastName = "User",
            UserRoles = [ new UserRole { UserId = 1, RoleId = 1, Role = _logistRole } ]
        };
        _adminUser = new User
        {
            Id = 2,
            Email = "admin@example.com",
            Password = hpw,
            FirstName = "Adm",
            LastName = "User",
            UserRoles = [ new UserRole { UserId = 2, RoleId = 2, Role = _adminRole } ]
        };
        _dbContext.Users.AddRange(_logistUser, _adminUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<RegisterController>();
        _controller = new RegisterController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
        _controller = new RegisterController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [Test]
    public async Task GetRegisters_ReturnsData_ForLogist()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters();
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.InstanceOf<IEnumerable<RegisterItem>>());
    }

    [Test]
    public async Task GetRegisters_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.GetRegisters();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UploadRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2); // Admin user
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);
        var result = await _controller.UploadRegister(mockFile.Object);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenNoFileUploaded()
    {
        SetCurrentUserId(1); // Logist user
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);
        var result = await _controller.UploadRegister(mockFile.Object);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("No file was uploaded"));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenEmptyFileUploaded()
    {
        SetCurrentUserId(1); // Logist user
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("No file was uploaded"));
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenUnsupportedFileType()
    {
        SetCurrentUserId(1); // Logist user
        var mockFile = CreateMockFile("test.pdf", "application/pdf", new byte[] { 0x01 });

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("Unsupported file type"));
    }

    [Test]
    public async Task UploadRegister_ReturnsSuccess_WhenExcelFileUploaded()
    {
        SetCurrentUserId(1); // Logist user  

        string testFilePath = Path.Combine(testDataDir, "Реестр_207730349.xlsx");       
        byte[] excelContent;

        try
        {
            excelContent = File.ReadAllBytes(testFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test file not found at {testFilePath}: {ex.Message}");
            return;
        }

        var mockFile = CreateMockFile("Реестр_207730349.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelContent);
        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task UploadRegister_ReturnsBadRequest_WhenZipWithoutExcelUploaded()
    {
        SetCurrentUserId(1); // Logist user

        // Create a real ZIP in memory without any Excel files
        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("test.txt");
            using var entryStream = entry.Open();
            byte[] textContent = System.Text.Encoding.UTF8.GetBytes("Test content");
            entryStream.Write(textContent, 0, textContent.Length);
        }

        var mockFile = CreateMockFile("test.zip", "application/zip", zipStream.ToArray());

        var result = await _controller.UploadRegister(mockFile.Object);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var error = obj.Value as ErrMessage;
        Assert.That(error!.Msg, Does.Contain("No Excel file found"));
    }

    [Test]
    public async Task UploadRegister_ReturnsSuccess_WhenZipWithExcelUploaded()
    {
        SetCurrentUserId(1); // Logist user

        // Load test zip file from test.data folder
        string testFilePath = Path.Combine(testDataDir, "Реестр_207730349.zip");

        byte[] zipContent;

        try
        {
            zipContent = File.ReadAllBytes(testFilePath);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test file not found at {testFilePath}: {ex.Message}");
            return;
        }

        var mockFile = CreateMockFile("Реестр_207730349.zip", "application/zip", zipContent);

        var result = await _controller.UploadRegister(mockFile.Object);
    }

    // Helper method to create mock IFormFile objects
    private static Mock<IFormFile> CreateMockFile(string fileName, string contentType, byte[] content)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((stream, token) => {
                stream.Write(content, 0, content.Length);
            })
            .Returns(Task.CompletedTask);

        return mockFile;
    }
}
