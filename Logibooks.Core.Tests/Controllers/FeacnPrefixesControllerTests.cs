// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
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
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
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

using Moq;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Tests.Controllers;

[TestFixture]
public class FeacnPrefixesControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<FeacnPrefixesController> _logger;
    private IUserInformationService _userService;
    private FeacnPrefixesController _controller;
    private Role _adminRole;
    private Role _logistRole;
    private User _adminUser;
    private User _logistUser;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"feacn_prefixes_controller_db_{System.Guid.NewGuid()}")
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
        _logger = new LoggerFactory().CreateLogger<FeacnPrefixesController>();
        _userService = new UserInformationService(_dbContext);
        _controller = new FeacnPrefixesController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger);
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
        _controller = new FeacnPrefixesController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger);
    }

    [Test]
    public async Task GetPrefixes_ReturnsItems_ForLogist()
    {
        SetCurrentUserId(2);
        var p1 = new FeacnPrefix { Id = 1, Code = "10" };
        var p2 = new FeacnPrefix { Id = 2, Code = "20", FeacnOrderId = 1 };
        var ex = new FeacnPrefixException { Id = 3, Code = "1001", FeacnPrefixId = 1, FeacnPrefix = p1 };
        _dbContext.FeacnPrefixes.AddRange(p1, p2);
        _dbContext.FeacnPrefixExceptions.Add(ex);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetPrefixes();

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(1));
        Assert.That(result.Value!.First().Exceptions.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task GetPrefixes_Returns403_ForNonLogist()
    {
        SetCurrentUserId(1); // admin but not logist
        var result = await _controller.GetPrefixes();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task CreateUpdateDelete_Work_ForAdmin()
    {
        SetCurrentUserId(1);
        var dto = new FeacnPrefixCreateDto { Code = "30", Exceptions = ["3001", "3002"] };
        var created = await _controller.CreatePrefix(dto);
        Assert.That(created.Result, Is.TypeOf<CreatedAtActionResult>());
        var refId = ((Reference)((CreatedAtActionResult)created.Result!).Value!).Id;
        var prefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == refId);
        Assert.That(prefix.FeacnPrefixExceptions.Count, Is.EqualTo(2));

        var updDto = new FeacnPrefixCreateDto { Id = refId, Code = "31", Exceptions = ["3101"] };
        var upd = await _controller.UpdatePrefix(refId, updDto);
        Assert.That(upd, Is.TypeOf<NoContentResult>());
        var updPrefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == refId);
        Assert.That(updPrefix.Code, Is.EqualTo("31"));
        Assert.That(updPrefix.FeacnPrefixExceptions.Count, Is.EqualTo(1));

        var del = await _controller.DeletePrefix(refId);
        Assert.That(del, Is.TypeOf<NoContentResult>());
        Assert.That(_dbContext.FeacnPrefixes.Any(p => p.Id == refId), Is.False);
    }

    [Test]
    public async Task CreatePrefix_Returns403_ForNonAdmin()
    {
        SetCurrentUserId(2); // logist only
        var dto = new FeacnPrefixCreateDto { Code = "40" };
        var result = await _controller.CreatePrefix(dto);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdatePrefix_Returns403_WhenHasOrder()
    {
        SetCurrentUserId(1);
        var prefix = new FeacnPrefix { Id = 5, Code = "50", FeacnOrderId = 1 };
        _dbContext.FeacnPrefixes.Add(prefix);
        await _dbContext.SaveChangesAsync();

        var dto = new FeacnPrefixCreateDto { Id = 5, Code = "51" };
        var result = await _controller.UpdatePrefix(5, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetPrefix_ReturnsItem_ForLogist()
    {
        SetCurrentUserId(2);
        var prefix = new FeacnPrefix { Id = 6, Code = "60" };
        var ex = new FeacnPrefixException { Id = 7, Code = "6001", FeacnPrefixId = 6, FeacnPrefix = prefix };
        _dbContext.FeacnPrefixes.Add(prefix);
        _dbContext.FeacnPrefixExceptions.Add(ex);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetPrefix(6);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(6));
        Assert.That(result.Value!.Exceptions.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task GetPrefix_Returns403_ForNonLogist()
    {
        SetCurrentUserId(1); // admin but not logist
        var prefix = new FeacnPrefix { Id = 8, Code = "80" };
        _dbContext.FeacnPrefixes.Add(prefix);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetPrefix(8);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetPrefix_Returns404_WhenNotFound()
    {
        SetCurrentUserId(2);
        var result = await _controller.GetPrefix(999);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetPrefix_Returns403_WhenHasOrder()
    {
        SetCurrentUserId(2);
        var prefix = new FeacnPrefix { Id = 9, Code = "90", FeacnOrderId = 2 };
        _dbContext.FeacnPrefixes.Add(prefix);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetPrefix(9);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdatePrefix_Returns403_ForNonAdmin()
    {
        SetCurrentUserId(2); // logist only
        var prefix = new FeacnPrefix { Id = 10, Code = "100" };
        _dbContext.FeacnPrefixes.Add(prefix);
        await _dbContext.SaveChangesAsync();

        var dto = new FeacnPrefixCreateDto { Id = 10, Code = "101" };
        var result = await _controller.UpdatePrefix(10, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdatePrefix_Returns404_WhenNotFound()
    {
        SetCurrentUserId(1);
        var dto = new FeacnPrefixCreateDto { Id = 11, Code = "110" };
        var result = await _controller.UpdatePrefix(11, dto);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task DeletePrefix_Returns403_ForNonAdmin()
    {
        SetCurrentUserId(2);
        var prefix = new FeacnPrefix { Id = 14, Code = "140" };
        _dbContext.FeacnPrefixes.Add(prefix);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeletePrefix(14);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeletePrefix_Returns404_WhenNotFound()
    {
        SetCurrentUserId(1);
        var result = await _controller.DeletePrefix(999);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task DeletePrefix_Returns403_WhenHasOrder()
    {
        SetCurrentUserId(1);
        var prefix = new FeacnPrefix { Id = 15, Code = "150", FeacnOrderId = 3 };
        _dbContext.FeacnPrefixes.Add(prefix);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeletePrefix(15);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    // New tests for exception code deduplication (silent handling)

    [Test]
    public async Task CreatePrefix_SilentlyDeduplicatesExceptionCodes()
    {
        SetCurrentUserId(1);
        
        // Create prefix with duplicate exception codes - should silently deduplicate
        var dto = new FeacnPrefixCreateDto 
        { 
            Code = "UNIQUE001", 
            Exceptions = ["EXC001", "EXC002", "EXC001", "EXC003"] // EXC001 is duplicated
        };
        var result = await _controller.CreatePrefix(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        
        var refId = ((Reference)created!.Value!).Id;
        var createdPrefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == refId);
        
        // Should only have unique exception codes
        Assert.That(createdPrefix.FeacnPrefixExceptions.Count, Is.EqualTo(3));
        var exceptionCodes = createdPrefix.FeacnPrefixExceptions.Select(e => e.Code).OrderBy(c => c).ToList();
        Assert.That(exceptionCodes, Is.EqualTo(new[] { "EXC001", "EXC002", "EXC003" }));
    }

    [Test]
    public async Task CreatePrefix_SilentlyDeduplicatesExceptionCodes_CaseInsensitive()
    {
        SetCurrentUserId(1);
        
        // Create prefix with case-insensitive duplicate exception codes - should silently deduplicate
        var dto = new FeacnPrefixCreateDto 
        { 
            Code = "UNIQUE002", 
            Exceptions = ["exc001", "EXC002", "EXC001", "exc002"] // Case-insensitive duplicates
        };
        var result = await _controller.CreatePrefix(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        
        var refId = ((Reference)created!.Value!).Id;
        var createdPrefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == refId);
        
        // Should keep first occurrence of each unique code in original order
        Assert.That(createdPrefix.FeacnPrefixExceptions.Count, Is.EqualTo(2));
        var exceptionCodes = createdPrefix.FeacnPrefixExceptions.Select(e => e.Code).ToList();
        Assert.That(exceptionCodes, Is.EqualTo(new[] { "exc001", "EXC002" })); // First occurrence kept in original order
    }

    [Test]
    public async Task CreatePrefix_SilentlyDeduplicatesExceptionCodes_WithWhitespace()
    {
        SetCurrentUserId(1);
        
        // Create prefix with whitespace-trimmed duplicate exception codes - should silently deduplicate
        var dto = new FeacnPrefixCreateDto 
        { 
            Code = "UNIQUE003", 
            Exceptions = ["EXC001", " EXC002 ", "EXC001 ", " ", "", "EXC003"] // Whitespace and empty codes
        };
        var result = await _controller.CreatePrefix(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        
        var refId = ((Reference)created!.Value!).Id;
        var createdPrefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == refId);
        
        // Should have trimmed, unique, non-empty codes
        Assert.That(createdPrefix.FeacnPrefixExceptions.Count, Is.EqualTo(3));
        var exceptionCodes = createdPrefix.FeacnPrefixExceptions.Select(e => e.Code).OrderBy(c => c).ToList();
        Assert.That(exceptionCodes, Is.EqualTo(new[] { "EXC001", "EXC002", "EXC003" }));
    }

    [Test]
    public async Task CreatePrefix_AllowsEmptyAndNullExceptionCodes()
    {
        SetCurrentUserId(1);
        
        // Create prefix with empty/null exception codes (should be filtered out)
        var dto = new FeacnPrefixCreateDto 
        { 
            Code = "UNIQUE004", 
            Exceptions = ["EXC001", "", " ", "EXC002"] // Empty and whitespace-only codes should be ignored
        };
        var result = await _controller.CreatePrefix(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        
        var refId = ((Reference)created!.Value!).Id;
        var createdPrefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == refId);
        
        // Should only have non-empty exception codes
        Assert.That(createdPrefix.FeacnPrefixExceptions.Count, Is.EqualTo(2));
        var exceptionCodes = createdPrefix.FeacnPrefixExceptions.Select(e => e.Code).OrderBy(c => c).ToList();
        Assert.That(exceptionCodes, Is.EqualTo(new[] { "EXC001", "EXC002" }));
    }

    [Test]
    public async Task UpdatePrefix_SilentlyDeduplicatesExceptionCodes()
    {
        SetCurrentUserId(1);
        
        // Create prefix first
        var prefix = new FeacnPrefix { Id = 200, Code = "UPDATE001", FeacnOrderId = null };
        _dbContext.FeacnPrefixes.Add(prefix);
        await _dbContext.SaveChangesAsync();

        // Update with duplicate exception codes - should silently deduplicate
        var dto = new FeacnPrefixCreateDto 
        { 
            Id = 200, 
            Code = "UPDATE001", 
            Exceptions = ["UPD001", "UPD002", "upd001", "UPD003", "UPD002"] // Multiple duplicates
        };
        var result = await _controller.UpdatePrefix(200, dto);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        
        var updatedPrefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == 200);
        
        // Should have deduplicated codes, keeping first occurrence
        Assert.That(updatedPrefix.FeacnPrefixExceptions.Count, Is.EqualTo(3));
        var exceptionCodes = updatedPrefix.FeacnPrefixExceptions.Select(e => e.Code).OrderBy(c => c).ToList();
        Assert.That(exceptionCodes, Is.EqualTo(new[] { "UPD001", "UPD002", "UPD003" }));
    }

    [Test]
    public async Task UpdatePrefix_AllowsUniqueExceptionCodes()
    {
        SetCurrentUserId(1);
        
        // Create prefix with some exceptions
        var prefix = new FeacnPrefix { Id = 201, Code = "UPDATE002", FeacnOrderId = null };
        var ex1 = new FeacnPrefixException { Id = 301, Code = "OLD001", FeacnPrefixId = 201, FeacnPrefix = prefix };
        var ex2 = new FeacnPrefixException { Id = 302, Code = "OLD002", FeacnPrefixId = 201, FeacnPrefix = prefix };
        _dbContext.FeacnPrefixes.Add(prefix);
        _dbContext.FeacnPrefixExceptions.AddRange(ex1, ex2);
        await _dbContext.SaveChangesAsync();

        // Update with new unique exception codes
        var dto = new FeacnPrefixCreateDto 
        { 
            Id = 201, 
            Code = "UPDATE002", 
            Exceptions = ["NEW001", "NEW002", "NEW003"] // All unique codes
        };
        var result = await _controller.UpdatePrefix(201, dto);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        
        var updatedPrefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == 201);
        
        Assert.That(updatedPrefix.FeacnPrefixExceptions.Count, Is.EqualTo(3));
        var exceptionCodes = updatedPrefix.FeacnPrefixExceptions.Select(e => e.Code).OrderBy(c => c).ToList();
        Assert.That(exceptionCodes, Is.EqualTo(new[] { "NEW001", "NEW002", "NEW003" }));
    }

    [Test]
    public async Task CreatePrefix_PreservesOrderForUniqueExceptionCodes()
    {
        SetCurrentUserId(1);
        
        // Test that the first occurrence of each unique code is preserved
        var dto = new FeacnPrefixCreateDto 
        { 
            Code = "ORDER001", 
            Exceptions = ["FIRST", "SECOND", "first", "THIRD", "second", "FOURTH"] // Mixed case duplicates
        };
        var result = await _controller.CreatePrefix(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        
        var refId = ((Reference)created!.Value!).Id;
        var createdPrefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == refId);
        
        // Should preserve the order of first occurrences
        Assert.That(createdPrefix.FeacnPrefixExceptions.Count, Is.EqualTo(4));
        var exceptionCodes = createdPrefix.FeacnPrefixExceptions.Select(e => e.Code).ToList();
        Assert.That(exceptionCodes, Is.EqualTo(new[] { "FIRST", "SECOND", "THIRD", "FOURTH" }));
    }

    [Test]
    public async Task CreatePrefix_AllowsDuplicateExceptionCodes_ForAutoGeneratedPrefixes()
    {
        SetCurrentUserId(1);
        
        // This test ensures that our deduplication only applies to user-created prefixes
        // Auto-generated prefixes (FeacnOrderId != null) are not subject to our deduplication logic
        
        // Create an auto-generated prefix with duplicate exception codes (should be allowed by the system)
        var autoPrefix = new FeacnPrefix 
        { 
            Id = 300, 
            Code = "AUTO001", 
            FeacnOrderId = 5 // Auto-generated 
        };
        var ex1 = new FeacnPrefixException { Id = 401, Code = "DUP001", FeacnPrefixId = 300, FeacnPrefix = autoPrefix };
        var ex2 = new FeacnPrefixException { Id = 402, Code = "DUP001", FeacnPrefixId = 300, FeacnPrefix = autoPrefix }; // Duplicate code
        _dbContext.FeacnPrefixes.Add(autoPrefix);
        _dbContext.FeacnPrefixExceptions.AddRange(ex1, ex2);
        await _dbContext.SaveChangesAsync();

        // Now create a user prefix - our deduplication should apply to this one
        var dto = new FeacnPrefixCreateDto 
        { 
            Code = "USER001", 
            Exceptions = ["USER_EXC001", "USER_EXC002", "user_exc001"] // Should be deduplicated
        };
        var result = await _controller.CreatePrefix(dto);

        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        
        var created = result.Result as CreatedAtActionResult;
        var refId = ((Reference)created!.Value!).Id;
        var userPrefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == refId);
        
        // User prefix should have deduplicated codes
        Assert.That(userPrefix.FeacnPrefixExceptions.Count, Is.EqualTo(2));
        var userExceptionCodes = userPrefix.FeacnPrefixExceptions.Select(e => e.Code).OrderBy(c => c).ToList();
        Assert.That(userExceptionCodes, Is.EqualTo(new[] { "USER_EXC001", "USER_EXC002" }));
        
        // Verify that auto-generated prefix still exists with duplicate exception codes
        var existingAutoPrefix = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstAsync(p => p.Id == 300);
        Assert.That(existingAutoPrefix.FeacnPrefixExceptions.Count, Is.EqualTo(2));
        Assert.That(existingAutoPrefix.FeacnPrefixExceptions.All(e => e.Code == "DUP001"), Is.True);
    }

    [Test]
    public async Task CreatePrefix_HandlesEdgeCases_ForUniqueness()
    {
        SetCurrentUserId(1);
        
        // Test case sensitivity and whitespace handling
        var prefix1 = new FeacnPrefix { Id = 107, Code = "testcode", FeacnOrderId = null };
        _dbContext.FeacnPrefixes.Add(prefix1);
        await _dbContext.SaveChangesAsync();

        // Try to create with exact same code (should fail)
        var dto1 = new FeacnPrefixCreateDto { Code = "testcode" };
        var result1 = await _controller.CreatePrefix(dto1);
        Assert.That(result1.Result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result1.Result!).StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));

        // Create with different case (should succeed - EF Core is case-sensitive for string comparisons)
        var dto2 = new FeacnPrefixCreateDto { Code = "TESTCODE" };
        var result2 = await _controller.CreatePrefix(dto2);
        Assert.That(result2.Result, Is.TypeOf<CreatedAtActionResult>());
    }
}
