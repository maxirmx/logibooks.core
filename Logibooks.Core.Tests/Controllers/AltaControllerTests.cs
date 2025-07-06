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
}
