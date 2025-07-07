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
    public async Task Items_CrudOperations_ReturnForbidden_ForNonAdmin_ExceptGet()
    {
        SetCurrentUserId(2);
        var dto = new AltaItemDto { Id = 1 };

        var result1 = await _controller.GetItems();
        Assert.That(result1.Result, Is.Null);

        var result2 = await _controller.GetItem(1);
        Assert.That(result2.Result, Is.TypeOf<ObjectResult>());
        var obj = result2.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));

        var result3 = await _controller.CreateItem(dto);
        Assert.That(result3.Result, Is.TypeOf<ObjectResult>());
        obj = result3.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));


        var result4 = await _controller.UpdateItem(1, dto);
        Assert.That(result4, Is.TypeOf<ObjectResult>());
        obj = result4 as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));


        var result5 = await _controller.DeleteItem(1);
        Assert.That(result5, Is.TypeOf<ObjectResult>());
        obj = result5 as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Exceptions_CrudOperations_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var create = new AltaExceptionDto { Url = "u", Code = "c", Name = "n" };
        var created = await _controller.CreateException(create);

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
    public async Task Exceptions_CrudOperations_ReturnForbidden_ForNonAdmin_ExceptGet()
    {
        SetCurrentUserId(2);
        var dto = new AltaExceptionDto { Id = 1 };
        
        var result1 = await _controller.GetExceptions();
        Assert.That(result1.Result, Is.Null);

        var result2 = await _controller.GetException(1);
        Assert.That(result2.Result, Is.TypeOf<ObjectResult>());
        var obj = result2.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));

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

    [Test]
    public async Task CreateItem_ReturnsConflict_WhenCodeAlreadyExists()
    {
        // Arrange
        SetCurrentUserId(1);
        var existingItem = new AltaItemDto { Url = "u1", Code = "123", Name = "existing" };
        await _controller.CreateItem(existingItem);

        var duplicateItem = new AltaItemDto { Url = "u2", Code = "123", Name = "duplicate" };

        // Act
        var result = await _controller.CreateItem(duplicateItem);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));

        var errMessage = obj.Value as ErrMessage;
        Assert.That(errMessage!.Msg, Contains.Substring("123"));
        Assert.That(errMessage.Msg, Contains.Substring("уже существует"));
    }

    [Test]
    public async Task CreateItem_ReturnsConflict_WhenCodeExistsCaseInsensitive()
    {
        // Arrange
        SetCurrentUserId(1);
        var existingItem = new AltaItemDto { Url = "u1", Code = "123", Name = "existing" };
        await _controller.CreateItem(existingItem);

        var duplicateItem = new AltaItemDto { Url = "u2", Code = "123", Name = "duplicate" };

        // Act
        var result = await _controller.CreateItem(duplicateItem);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task UpdateItem_ReturnsConflict_WhenChangingToExistingCode()
    {
        // Arrange
        SetCurrentUserId(1);
        var item1 = new AltaItemDto { Url = "u1", Code = "001", Name = "item1" };
        var item2 = new AltaItemDto { Url = "u2", Code = "002", Name = "item2" };

        var created1 = await _controller.CreateItem(item1);
        var created2 = await _controller.CreateItem(item2);

        var id1 = ((created1.Result as CreatedAtActionResult)!.Value as AltaItemDto)!.Id;
        var id2 = ((created2.Result as CreatedAtActionResult)!.Value as AltaItemDto)!.Id;

        // Act - try to change item2's code to item1's code
        item2.Id = id2;
        item2.Code = "001"; // This should conflict
        var result = await _controller.UpdateItem(id2, item2);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));

        var errMessage = obj.Value as ErrMessage;
        Assert.That(errMessage!.Msg, Contains.Substring("001"));
    }

    [Test]
    public async Task UpdateItem_Succeeds_WhenKeepingSameCode()
    {
        // Arrange
        SetCurrentUserId(1);
        var item = new AltaItemDto { Url = "u1", Code = "123", Name = "original" };
        var created = await _controller.CreateItem(item);
        var id = ((created.Result as CreatedAtActionResult)!.Value as AltaItemDto)!.Id;

        // Act - update same item keeping the same code
        item.Id = id;
        item.Name = "updated";
        var result = await _controller.UpdateItem(id, item);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task CreateException_ReturnsConflict_WhenCodeAlreadyExists()
    {
        // Arrange
        SetCurrentUserId(1);
        var existingException = new AltaExceptionDto { Url = "u1", Code = "123", Name = "existing" };
        await _controller.CreateException(existingException);

        var duplicateException = new AltaExceptionDto { Url = "u2", Code = "123", Name = "duplicate" };

        // Act
        var result = await _controller.CreateException(duplicateException);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));

        var errMessage = obj.Value as ErrMessage;
        Assert.That(errMessage!.Msg, Contains.Substring("123"));
        Assert.That(errMessage.Msg, Contains.Substring("уже существует"));
    }

    [Test]
    public async Task CreateException_ReturnsConflict_WhenCodeExistsCaseInsensitive()
    {
        // Arrange
        SetCurrentUserId(1);
        var existingException = new AltaExceptionDto { Url = "u1", Code = "456", Name = "existing" };
        await _controller.CreateException(existingException);

        var duplicateException = new AltaExceptionDto { Url = "u2", Code = "456", Name = "duplicate" };

        // Act
        var result = await _controller.CreateException(duplicateException);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task UpdateException_ReturnsConflict_WhenChangingToExistingCode()
    {
        // Arrange
        SetCurrentUserId(1);
        var exc1 = new AltaExceptionDto { Url = "u1", Code = "001", Name = "exc1" };
        var exc2 = new AltaExceptionDto { Url = "u2", Code = "002", Name = "exc2" };

        var created1 = await _controller.CreateException(exc1);
        var created2 = await _controller.CreateException(exc2);

        var id1 = ((created1.Result as CreatedAtActionResult)!.Value as AltaExceptionDto)!.Id;
        var id2 = ((created2.Result as CreatedAtActionResult)!.Value as AltaExceptionDto)!.Id;

        // Act - try to change exc2's code to exc1's code
        exc2.Id = id2;
        exc2.Code = "001"; // This should conflict
        var result = await _controller.UpdateException(id2, exc2);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));

        var errMessage = obj.Value as ErrMessage;
        Assert.That(errMessage!.Msg, Contains.Substring("001"));
    }

    [Test]
    public async Task UpdateException_Succeeds_WhenKeepingSameCode()
    {
        // Arrange
        SetCurrentUserId(1);
        var exception = new AltaExceptionDto { Url = "u1", Code = "123", Name = "original" };
        var created = await _controller.CreateException(exception);
        var id = ((created.Result as CreatedAtActionResult)!.Value as AltaExceptionDto)!.Id;

        // Act - update same exception keeping the same code
        exception.Id = id;
        exception.Name = "updated";
        var result = await _controller.UpdateException(id, exception);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());
    }



}
