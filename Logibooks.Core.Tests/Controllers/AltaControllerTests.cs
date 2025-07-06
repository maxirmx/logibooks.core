using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Logibooks.Core.Tests.Controllers;

public class FakeHandler : HttpMessageHandler
{
    private readonly string _html;
    public FakeHandler(string html) { _html = html; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_html)
        };
        return Task.FromResult(resp);
    }
}

[TestFixture]
public class AltaControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<AltaController> _logger;
    private AltaController _controller;
    private Role _adminRole;
    private Role _userRole;
    private User _adminUser;
    private User _regularUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"alta_controller_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _adminRole = new Role { Id = 1, Name = "administrator", Title = "Admin" };
        _userRole = new Role { Id = 2, Name = "user", Title = "User" };
        _dbContext.Roles.AddRange(_adminRole, _userRole);

        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _adminUser = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = hpw,
            UserRoles = [ new UserRole { UserId = 1, RoleId = 1, Role = _adminRole } ]
        };
        _regularUser = new User
        {
            Id = 2,
            Email = "user@example.com",
            Password = hpw,
            UserRoles = [ new UserRole { UserId = 2, RoleId = 2, Role = _userRole } ]
        };
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<AltaController>();
        _controller = new AltaController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private void SetCurrentUserId(int id, HttpClient? client = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = id;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(ctx);
        _controller = new AltaController(_mockHttpContextAccessor.Object, _dbContext, _logger, client);
    }

    [Test]
    public async Task Parse_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var result = await _controller.Parse();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Parse_AddsItems_ForAdmin()
    {
        var html = @"<table><tr><td>Prod1</td><td>1234 56 789 0</td><td>c1</td></tr></table>";
        var client = new HttpClient(new FakeHandler(html));
        SetCurrentUserId(1, client);
        var result = await _controller.Parse();
        Assert.That(result.Value, Is.EqualTo(2));
        Assert.That(await _dbContext.AltaItems.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task Parse_AddsExceptions_ForAdmin()
    {
        var html = @"<table><tr><td>Prod1</td><td>1234 56 789 0 (за исключением 1234 56 000 0)</td><td>c1</td></tr></table>";
        var client = new HttpClient(new FakeHandler(html));
        SetCurrentUserId(1, client);

        // Pre-compute expected values based on known parser behavior
        const int numberOfUrls = 4; // AltaController.Parse() processes 4 URLs
        const int rowsPerUrl = 1;    // Our test HTML has 1 data row
        const int itemsPerRow = 1;   // Each row creates 1 item
        const int exceptionsPerRow = 1; // This row has "за исключением" so creates 1 exception

        var expectedItems = numberOfUrls * rowsPerUrl * itemsPerRow;
        var expectedExceptions = numberOfUrls * rowsPerUrl * exceptionsPerRow;
        var expectedTotalReturned = expectedItems; // Parse() returns item count

        var result = await _controller.Parse();
        Assert.That(result.Value, Is.EqualTo(expectedTotalReturned),
            $"Expected {expectedTotalReturned} items returned from Parse()");
        Assert.That(await _dbContext.AltaItems.CountAsync(), Is.EqualTo(expectedItems),
            $"Expected {expectedItems} items in database");
        Assert.That(await _dbContext.AltaExceptions.CountAsync(), Is.EqualTo(expectedExceptions),
            $"Expected {expectedExceptions} exceptions in database");
    }

    [Test]
    public async Task CrudOperations_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var create = new AltaItemDto { Url = "u", Code = "c", Name = "n" };
        var created = await _controller.CreateItem(create);
        var refId = ((created.Result as CreatedAtActionResult)!.Value as AltaItemDto)!.Id;
        var getItem = await _controller.GetItem(refId);
        Assert.That(getItem.Value!.Name, Is.EqualTo("n"));

        create.Id = refId;
        create.Name = "n2";
        await _controller.UpdateItem(refId, create);
        var updated = await _controller.GetItem(refId);
        Assert.That(updated.Value!.Name, Is.EqualTo("n2"));

        var items = await _controller.GetItems();
        Assert.That(items.Value!.Count(), Is.EqualTo(1));

        await _controller.DeleteItem(refId);
        Assert.That(await _dbContext.AltaItems.FindAsync(refId), Is.Null);
    }

    [Test]
    public async Task CrudOperations_ReturnForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new AltaItemDto { Id = 1 };
        Assert.That((await _controller.GetItems()).Result, Is.TypeOf<ObjectResult>());
        Assert.That((await _controller.GetItem(1)).Result, Is.TypeOf<ObjectResult>());
        Assert.That((await _controller.CreateItem(dto)).Result, Is.TypeOf<ObjectResult>());
        Assert.That(await _controller.UpdateItem(1, dto), Is.TypeOf<ObjectResult>());
        Assert.That(await _controller.DeleteItem(1), Is.TypeOf<ObjectResult>());
    }

    [Test]
    public async Task Exceptions_CrudOperations_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var create = new AltaExceptionDto { Url = "u", Code = "c", Name = "n" };
        var created = await _controller.CreateException(create);

        // Replace Assert.IsType with Assert.That and appropriate checks
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
        var resultAction = created.Result as CreatedAtActionResult;
        Assert.That(resultAction!.Value, Is.TypeOf<AltaExceptionDto>());
        var createdDto = resultAction.Value as AltaExceptionDto;
        var refId = createdDto!.Id;

        var getItem = await _controller.GetException(refId);
        Assert.That(getItem.Value!.Name, Is.EqualTo("n"));

        create.Id = refId;
        create.Name = "n2";
        await _controller.UpdateException(refId, create);
        var updated = await _controller.GetException(refId);
        Assert.That(updated.Value!.Name, Is.EqualTo("n2"));

        var items = await _controller.GetExceptions();
        Assert.That(items.Value!.Count(), Is.EqualTo(1));

        await _controller.DeleteException(refId);
        Assert.That(await _dbContext.AltaExceptions.FindAsync(refId), Is.Null);
    }

    [Test]
    public async Task Exceptions_CrudOperations_ReturnForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new AltaExceptionDto { Id = 1 };
        
        var result1 = await _controller.GetExceptions();
        Assert.That(result1.Result, Is.TypeOf<ObjectResult>());
        var obj = result1.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));

        var result2 = await _controller.GetException(1);
        Assert.That(result2.Result, Is.TypeOf<ObjectResult>());
        obj = result2.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));

        var result3 = await _controller.CreateException(dto);
        Assert.That(result3.Result, Is.TypeOf<ObjectResult>());
        obj = result3.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));


        var result4 = await _controller.UpdateException(1, dto);
        Assert.That(result4, Is.TypeOf<ObjectResult>());
        obj = result4 as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));


        var result5 = await _controller.DeleteException(1);
        Assert.That(result5, Is.TypeOf<ObjectResult>());
        obj = result5 as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }
}
