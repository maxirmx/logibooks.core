// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks core application
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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class UsersControllerTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. 
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private Mock<ILogger<UsersController>> _mockLogger;
    private AppDbContext _dbContext;
    private UsersController _controller;
    private User _adminUser;
    private User _regularUser;
    private Role _adminRole;
    private Role _logistRole;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

    [SetUp]
    public void Setup()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"users_controller_test_db_{System.Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);

        // Add roles
        _adminRole = new Role { Id = 1, Name = "administrator", Title = "Администратор" };
        _logistRole = new Role { Id = 2, Name = "logist", Title = "Логист" };
        _dbContext.Roles.AddRange(_adminRole, _logistRole);

        // Setup users with hashed passwords
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword("password123");

        _adminUser = new User
        {
            Id = 1,
            Email = "admin@example.com",
            Password = hashedPassword,
            FirstName = "Admin",
            LastName = "User",
            Patronymic = "",
            UserRoles =
            [
                new UserRole { UserId = 1, RoleId = 1, Role = _adminRole }
            ]
        };

        _regularUser = new User
        {
            Id = 2,
            Email = "regular@example.com",
            Password = hashedPassword,
            FirstName = "Regular",
            LastName = "User",
            Patronymic = "",
            UserRoles =
            [
                new UserRole { UserId = 2, RoleId = 2, Role = _logistRole }
            ]
        };

        // Setup mocks
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<UsersController>>();

        // Save entities to database
        _dbContext.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region GetUsers Tests

    [Test]
    public async Task GetUsers_ReturnsAllUsers_WhenUserIsAdmin()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetUsers();

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.InstanceOf<IEnumerable<UserViewItem>>());
        var users = result.Value as IEnumerable<UserViewItem>;
        Assert.That(users, Has.Count.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task GetUsers_ReturnsForbidden_WhenUserIsNotAdmin()
    {
        // Arrange
        SetCurrentUserId(2); // Regular user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetUsers();

        // Assert
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    #endregion

    #region GetUser Tests

    [Test]
    public async Task GetUser_ReturnsUser_WhenUserIsAdmin()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetUser(2); // Getting regular user

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.TypeOf<UserViewItem>());
        var user = result.Value;
        Assert.That(user!.Id, Is.EqualTo(2));
        Assert.That(user.Email, Is.EqualTo("regular@example.com"));
    }

    [Test]
    public async Task GetUser_ReturnsUser_WhenUserAccessesSelf()
    {
        // Arrange
        SetCurrentUserId(2); // Regular user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetUser(2); // Getting self

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value, Is.TypeOf<UserViewItem>());
        var user = result.Value;
        Assert.That(user!.Id, Is.EqualTo(2));
        Assert.That(user.Email, Is.EqualTo("regular@example.com"));
    }

    [Test]
    public async Task GetUser_ReturnsForbidden_WhenNonAdminAccessesOtherUser()
    {
        // Arrange
        SetCurrentUserId(2); // Regular user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetUser(1); // Getting admin user

        // Assert
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetUser(999); // Non-existent user

        // Assert
        Assert.That(result.Result, Is.Not.Null);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    #endregion

    #region PostUser Tests

    [Test]
    public async Task PostUser_CreatesUser_WhenUserIsAdmin()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.Add(_adminUser);
        await _dbContext.SaveChangesAsync();

        var newUser = new User
        {
            Email = "new@example.com",
            Password = "newpassword",
            FirstName = "New",
            LastName = "User",
            Patronymic = ""
        };

        // Act
        var result = await _controller.PostUser(newUser);

        // Assert
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdAtActionResult = result.Result as CreatedAtActionResult;
        Assert.That(createdAtActionResult!.ActionName, Is.EqualTo(nameof(UsersController.GetUser)));
        Assert.That(createdAtActionResult.Value, Is.TypeOf<Reference>());

        var reference = createdAtActionResult.Value as Reference;
        Assert.That(reference!.Id, Is.GreaterThan(0));

        // Verify user was added to database
        var savedUser = await _dbContext.Users.FindAsync(reference.Id);
        Assert.That(savedUser, Is.Not.Null);
        Assert.That(savedUser!.Email, Is.EqualTo("new@example.com"));
        // Check that password was hashed
        Assert.That(BCrypt.Net.BCrypt.Verify("newpassword", savedUser.Password), Is.True);
    }

    [Test]
    public async Task PostUser_ReturnsForbidden_WhenUserIsNotAdmin()
    {
        // Arrange
        SetCurrentUserId(2); // Regular user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        var newUser = new User
        {
            Email = "new@example.com",
            Password = "newpassword",
            FirstName = "New",
            LastName = "User",
            Patronymic = ""
        };

        // Act
        var result = await _controller.PostUser(newUser);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PostUser_ReturnsConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        var newUser = new User
        {
            Email = "regular@example.com", // Already exists
            Password = "newpassword",
            FirstName = "New",
            LastName = "User",
            Patronymic = ""
        };

        // Act
        var result = await _controller.PostUser(newUser);

        // Assert
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var objectResult = result.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    #endregion

    #region PutUser Tests

    [Test]
    public async Task PutUser_UpdatesUser_WhenUserIsAdmin()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Roles = ["logist"]
        };

        // Act
        var result = await _controller.PutUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());

        // Verify user was updated
        var updatedUser = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == 2);

        Assert.That(updatedUser, Is.Not.Null);
        Assert.That(updatedUser!.FirstName, Is.EqualTo("Updated"));
        Assert.That(updatedUser.LastName, Is.EqualTo("Name"));
        Assert.That(updatedUser.Email, Is.EqualTo("updated@example.com"));
        Assert.That(updatedUser.UserRoles, Has.Count.EqualTo(1));
        Assert.That(updatedUser.UserRoles.First().Role!.Name, Is.EqualTo("logist"));
    }

    [Test]
    public async Task PutUser_UpdatesOwnData_WhenNotAdmin()
    {
        // Arrange
        SetCurrentUserId(2); // Regular user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            FirstName = "Self",
            LastName = "Updated",
            // Not changing roles as non-admin
        };

        // Act
        var result = await _controller.PutUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());

        // Verify user was updated
        var updatedUser = await _dbContext.Users.FindAsync(2);
        Assert.That(updatedUser, Is.Not.Null);
        Assert.That(updatedUser!.FirstName, Is.EqualTo("Self"));
        Assert.That(updatedUser.LastName, Is.EqualTo("Updated"));
    }

    [Test]
    public async Task PutUser_ReturnsForbidden_WhenNonAdminUpdatesOtherUser()
    {
        // Arrange
        SetCurrentUserId(2); // Regular user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            FirstName = "Try",
            LastName = "Update"
        };

        // Act
        var result = await _controller.PutUser(1, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task PutUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            FirstName = "Not",
            LastName = "Found"
        };

        // Act
        var result = await _controller.PutUser(999, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task PutUser_ReturnsConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            Email = "admin@example.com" // Already exists
        };

        // Act
        var result = await _controller.PutUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task PutUser_UpdatesPassword_WhenProvided()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            Password = "newpassword123"
        };

        // Act
        var result = await _controller.PutUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());

        // Verify password was updated and hashed
        var updatedUser = await _dbContext.Users.FindAsync(2);
        Assert.That(updatedUser, Is.Not.Null);
        Assert.That(BCrypt.Net.BCrypt.Verify("newpassword123", updatedUser!.Password), Is.True);
    }

    [Test]
    public async Task PutUser_RequiresAdmin_WhenChangingAdminRole()
    {
        // Arrange
        SetCurrentUserId(2); // Regular user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        var updateItem = new UserUpdateItem
        {
            FirstName = "Try",
            Roles = ["administrator"] // Trying to become admin
        };

        // Act
        var result = await _controller.PutUser(2, updateItem);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    #endregion

    #region DeleteUser Tests

    [Test]
    public async Task DeleteUser_DeletesUser_WhenUserIsAdmin()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteUser(2);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());

        // Verify user was deleted
        var deletedUser = await _dbContext.Users.FindAsync(2);
        Assert.That(deletedUser, Is.Null);
    }

    [Test]
    public async Task DeleteUser_ReturnsForbidden_WhenUserIsNotAdmin()
    {
        // Arrange
        SetCurrentUserId(2); // Regular user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteUser(1);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        SetCurrentUserId(1); // Admin user
        _dbContext.Users.AddRange(_adminUser, _regularUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.DeleteUser(999);

        // Assert
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var objectResult = result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    #endregion

    private void SetCurrentUserId(int userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["UserId"] = userId;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
        _controller = new UsersController(_mockHttpContextAccessor.Object, _dbContext, _mockLogger.Object);
    }
}
