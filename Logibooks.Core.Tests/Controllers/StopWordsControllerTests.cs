using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Linq;
using System.Threading.Tasks;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class StopWordsControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<StopWordsController> _logger;
    private StopWordsController _controller;
    private Role _adminRole;
    private Role _logistRole;
    private User _adminUser;
    private User _logistUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"stop_words_controller_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _adminRole = new Role { Id = 1, Name = "administrator", Title = "Администратор" };
        _logistRole = new Role { Id = 2, Name = "logist", Title = "Логист" };
        _dbContext.Roles.AddRange(_adminRole, _logistRole);

        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _adminUser = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = hpw,
            UserRoles = [new UserRole { UserId = 1, RoleId = 1, Role = _adminRole }]
        };
        _logistUser = new User
        {
            Id = 2,
            Email = "logist@example.com",
            Password = hpw,
            UserRoles = [new UserRole { UserId = 2, RoleId = 2, Role = _logistRole }]
        };
        _dbContext.Users.AddRange(_adminUser, _logistUser);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<StopWordsController>();
        _controller = new StopWordsController(_mockHttpContextAccessor.Object, _dbContext, _logger);
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
        _controller = new StopWordsController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [Test]
    public async Task GetStopWords_ReturnsAll_ForLogist()
    {
        SetCurrentUserId(2);
        _dbContext.StopWord.AddRange(new StopWord { Id = 1, Word = "a" }, new StopWord { Id = 2, Word = "b" });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetStopWords();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task CreateUpdateDelete_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var dto = new StopWordDto { Word = "test", ExactMatch = false };
        var created = await _controller.PostStopWord(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdDto = (created.Result as CreatedAtActionResult)!.Value as StopWordDto;
        Assert.That(createdDto!.Id, Is.GreaterThan(0));

        var id = createdDto.Id;
        createdDto.Word = "updated";
        var upd = await _controller.PutStopWord(id, createdDto);
        Assert.That(upd, Is.TypeOf<NoContentResult>());

        var del = await _controller.DeleteStopWord(id);
        Assert.That(del, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task Create_ReturnsForbidden_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var dto = new StopWordDto { Word = "w" };
        var result = await _controller.PostStopWord(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteStopWord_ReturnsConflict_WhenUsed()
    {
        SetCurrentUserId(1);
        var word = new StopWord { Id = 5, Word = "used" };
        var reg = new Register { Id = 1, FileName = "r" };
        var order = new WbrOrder { Id = 1, RegisterId = 1 };
        var link = new BaseOrderStopWord { BaseOrderId = 1, StopWordId = 5, BaseOrder = order, StopWord = word };
        _dbContext.StopWord.Add(word);
        _dbContext.Registers.Add(reg);
        _dbContext.Orders.Add(order);
        _dbContext.Add(link);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteStopWord(5);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task GetStopWord_ReturnsWord_WhenExists()
    {
        SetCurrentUserId(2);
        var word = new StopWord { Id = 6, Word = "find" };
        _dbContext.StopWord.Add(word);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetStopWord(6);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Word, Is.EqualTo("find"));
    }
}
