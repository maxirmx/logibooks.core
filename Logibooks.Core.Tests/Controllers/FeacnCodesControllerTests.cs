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

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
    private Mock<ILogger<FeacnCodesController>> _mockLogger;
    private FeacnCodesController _controller;
    private Role _userRole;
    private User _user;
    private Mock<IFeacnListProcessingService> _mockProcessingService;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"feacn_codes_db_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _userRole = new Role { Id = 1, Name = "user", Title = "User" };
        string hpw = BCrypt.Net.BCrypt.HashPassword("pwd");
        _user = new User
        {
            Id = 1,
            Email = "user@example.com",
            Password = hpw,
            UserRoles = [ new UserRole { UserId = 1, RoleId = 1, Role = _userRole } ]
        };
        _dbContext.Roles.Add(_userRole);
        _dbContext.Users.Add(_user);
        _dbContext.SaveChanges();

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext();
        ctx.Items["UserId"] = _user.Id;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(ctx);

        _mockLogger = new Mock<ILogger<FeacnCodesController>>();
        _mockProcessingService = new Mock<IFeacnListProcessingService>();
        _controller = new FeacnCodesController(_mockHttpContextAccessor.Object,
                                              _dbContext,
                                              _mockLogger.Object,
                                              _mockProcessingService.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = ctx
        };
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private static FeacnCode CreateCode(int id, string code, string name,
                                        int? parentId = null,
                                        DateOnly? fromDate = null)
    {
        return new FeacnCode
        {
            Id = id,
            Code = code,
            CodeEx = code,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ParentId = parentId,
            FromDate = fromDate
        };
    }

    [Test]
    public async Task Get_ReturnsCode_WhenExists()
    {
        var fc = CreateCode(1, "1234567890", "Test");
        _dbContext.FeacnCodes.Add(fc);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Get(1);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task Get_ReturnsNotFound_ForMissingOrInactive()
    {
        var fc = CreateCode(1, "1234567890", "Future", fromDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
        _dbContext.FeacnCodes.Add(fc);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.Get(1);
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));

        var result2 = await _controller.Get(99);
        Assert.That(result2.Result, Is.TypeOf<ObjectResult>());
        var obj2 = result2.Result as ObjectResult;
        Assert.That(obj2!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetByCode_ReturnsBadRequest_ForInvalidCode()
    {
        var result = await _controller.GetByCode("12AB");
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task GetByCode_ReturnsCode_WhenExists()
    {
        var fc = CreateCode(1, "1234567890", "Test");
        _dbContext.FeacnCodes.Add(fc);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetByCode("1234567890");
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Code, Is.EqualTo("1234567890"));
    }

    [Test]
    public async Task GetByCode_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.GetByCode("1234567890");
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Lookup_SearchesByPrefixOrName()
    {
        _dbContext.FeacnCodes.AddRange(
            CreateCode(1, "1234567890", "Alpha"),
            CreateCode(2, "1234560000", "Beta"),
            CreateCode(3, "9876543210", "Gamma")
        );
        await _dbContext.SaveChangesAsync();

        var byPrefix = await _controller.Lookup("1234");
        Assert.That(byPrefix.Value!.Count(), Is.EqualTo(2));

        var byName = await _controller.Lookup("gamma");
        Assert.That(byName.Value!.Single().Id, Is.EqualTo(3));

        var empty = await _controller.Lookup("   ");
        Assert.That(empty.Value, Is.Empty);
    }

    [Test]
    public async Task Children_ReturnsCorrectSets()
    {
        _dbContext.FeacnCodes.AddRange(
            CreateCode(1, "1111111111", "Root1"),
            CreateCode(2, "2222222222", "Root2"),
            CreateCode(3, "3333333333", "Child", parentId: 1)
        );
        await _dbContext.SaveChangesAsync();

        var roots = await _controller.Children(null);
        Assert.That(roots.Value!.Count(), Is.EqualTo(2));

        var children = await _controller.Children(1);
        Assert.That(children.Value!.Single().Id, Is.EqualTo(3));
    }

    [Test]
    public async Task Upload_ReturnsBadRequest_ForNullOrUnsupportedFile()
    {
        var resNull = await _controller.Upload(null!);
        Assert.That(resNull, Is.TypeOf<ObjectResult>());
        var objNull = resNull as ObjectResult;
        Assert.That(objNull!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));

        await using var ms = new MemoryStream([1, 2, 3]);
        IFormFile file = new FormFile(ms, 0, ms.Length, "file", "codes.txt");
        var resBad = await _controller.Upload(file);
        Assert.That(resBad, Is.TypeOf<ObjectResult>());
        var objBad = resBad as ObjectResult;
        Assert.That(objBad!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task Upload_ReturnsBadRequest_WhenZipWithoutExcel()
    {
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("inner.txt");
            await using var entryStream = entry.Open();
            var data = new byte[] { 1, 2, 3 };
            await entryStream.WriteAsync(data, 0, data.Length);
        }
        zipStream.Position = 0;
        IFormFile file = new FormFile(zipStream, 0, zipStream.Length, "file", "codes.zip");

        var result = await _controller.Upload(file);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task Upload_CallsService_ForXlsx()
    {
        var content = new byte[] { 1, 2, 3 };
        await using var ms = new MemoryStream(content);
        IFormFile file = new FormFile(ms, 0, ms.Length, "file", "codes.xlsx");

        var result = await _controller.Upload(file);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        _mockProcessingService.Verify(s => s.UploadFeacnCodesAsync(
            It.Is<byte[]>(b => b.SequenceEqual(content)),
            "codes.xlsx",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Upload_CallsService_ForZipWithExcel()
    {
        var excelBytes = new byte[] { 4, 5, 6 };
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("inner.xls");
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(excelBytes, 0, excelBytes.Length);
        }
        zipStream.Position = 0;
        IFormFile file = new FormFile(zipStream, 0, zipStream.Length, "file", "codes.zip");

        var result = await _controller.Upload(file);
        Assert.That(result, Is.TypeOf<NoContentResult>());
        _mockProcessingService.Verify(s => s.UploadFeacnCodesAsync(
            It.Is<byte[]>(b => b.SequenceEqual(excelBytes)),
            "inner.xls",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task BulkLookup_ReturnsEmpty_ForNullOrEmptyRequest()
    {
        var resultNull = await _controller.BulkLookup(null!);
        Assert.That(resultNull.Value, Is.Not.Null);
        Assert.That(resultNull.Value!.Results, Is.Empty);

        var requestEmpty = new BulkFeacnCodeRequestDto();
        var resultEmpty = await _controller.BulkLookup(requestEmpty);
        Assert.That(resultEmpty.Value, Is.Not.Null);
        Assert.That(resultEmpty.Value!.Results, Is.Empty);

        var requestEmptyArray = new BulkFeacnCodeRequestDto(Array.Empty<string>());
        var resultEmptyArray = await _controller.BulkLookup(requestEmptyArray);
        Assert.That(resultEmptyArray.Value, Is.Not.Null);
        Assert.That(resultEmptyArray.Value!.Results, Is.Empty);
    }

    [Test]
    public async Task BulkLookup_SkipsNullAndWhitespaceCodesAndReturnsEmptyForValid()
    {
        var request = new BulkFeacnCodeRequestDto(new[] { null!, "", "   ", "\t" });
        var result = await _controller.BulkLookup(request);
        
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Results, Is.Empty);
    }

    [Test]
    public async Task BulkLookup_ReturnsBadRequest_ForNonDigitCode()
    {
        var request = new BulkFeacnCodeRequestDto(new[] { "123456789A" }); // Contains letter
        var result = await _controller.BulkLookup(request);
        
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task BulkLookup_ReturnsFoundAndNotFoundCodes()
    {
        // Setup test data
        var existingCode1 = CreateCode(1, "1234567890", "Test Product 1");
        var existingCode2 = CreateCode(2, "0987654321", "Test Product 2");
        _dbContext.FeacnCodes.AddRange(existingCode1, existingCode2);
        await _dbContext.SaveChangesAsync();

        var request = new BulkFeacnCodeRequestDto(new[] 
        { 
            "1234567890", // exists
            "0987654321", // exists  
            "1111111111"  // doesn't exist
        });

        var result = await _controller.BulkLookup(request);
        
        Assert.That(result.Value, Is.Not.Null);
        var results = result.Value!.Results;
        Assert.That(results.Count, Is.EqualTo(3));
        
        // Check found codes
        Assert.That(results["1234567890"], Is.Not.Null);
        Assert.That(results["1234567890"]!.Code, Is.EqualTo("1234567890"));
        Assert.That(results["1234567890"]!.Name, Is.EqualTo("Test Product 1"));
        
        Assert.That(results["0987654321"], Is.Not.Null);
        Assert.That(results["0987654321"]!.Code, Is.EqualTo("0987654321"));
        Assert.That(results["0987654321"]!.Name, Is.EqualTo("Test Product 2"));
        
        // Check not found code
        Assert.That(results["1111111111"], Is.Null);
    }

    [Test]
    public async Task BulkLookup_RespectsDateFiltering()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureCode = CreateCode(1, "1234567890", "Future Code", fromDate: today.AddDays(1));
        var activeCode = CreateCode(2, "0987654321", "Active Code");
        
        _dbContext.FeacnCodes.AddRange(futureCode, activeCode);
        await _dbContext.SaveChangesAsync();

        var request = new BulkFeacnCodeRequestDto(new[] { "1234567890", "0987654321" });
        var result = await _controller.BulkLookup(request);
        
        Assert.That(result.Value, Is.Not.Null);
        var results = result.Value!.Results;
        Assert.That(results.Count, Is.EqualTo(2));
        
        // Future code should not be found (null)
        Assert.That(results["1234567890"], Is.Null);
        
        // Active code should be found
        Assert.That(results["0987654321"], Is.Not.Null);
        Assert.That(results["0987654321"]!.Name, Is.EqualTo("Active Code"));
    }

    [Test]
    public async Task BulkLookup_HandlesDuplicateCodes()
    {
        var existingCode = CreateCode(1, "1234567890", "Test Product");
        _dbContext.FeacnCodes.Add(existingCode);
        await _dbContext.SaveChangesAsync();

        var request = new BulkFeacnCodeRequestDto(new[] 
        { 
            "1234567890", 
            "1234567890" // duplicate
        });

        var result = await _controller.BulkLookup(request);
        
        Assert.That(result.Value, Is.Not.Null);
        var results = result.Value!.Results;
        
        // Should only have one entry in the dictionary
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results["1234567890"], Is.Not.Null);
        Assert.That(results["1234567890"]!.Name, Is.EqualTo("Test Product"));
    }

    [Test]
    public async Task BulkLookup_HandlesLargeNumberOfCodes()
    {
        // Create 100 test codes
        var testCodes = new List<FeacnCode>();
        for (int i = 0; i < 100; i++)
        {
            var code = i.ToString("D10"); // Pad with zeros to make 10 digits
            testCodes.Add(CreateCode(i + 1, code, $"Product {i}"));
        }
        
        _dbContext.FeacnCodes.AddRange(testCodes);
        await _dbContext.SaveChangesAsync();

        // Request all 100 codes plus some non-existent ones
        var requestCodes = testCodes.Select(c => c.Code).ToList();
        requestCodes.AddRange(new[] { "9999999990", "9999999991", "9999999992" });
        
        var request = new BulkFeacnCodeRequestDto(requestCodes.ToArray());
        var result = await _controller.BulkLookup(request);
        
        Assert.That(result.Value, Is.Not.Null);
        var results = result.Value!.Results;
        Assert.That(results.Count, Is.EqualTo(103)); // 100 existing + 3 non-existing
        
        // Check that all existing codes are found
        for (int i = 0; i < 100; i++)
        {
            var code = i.ToString("D10");
            Assert.That(results[code], Is.Not.Null);
            Assert.That(results[code]!.Name, Is.EqualTo($"Product {i}"));
        }
        
        // Check that non-existing codes are null
        Assert.That(results["9999999990"], Is.Null);
        Assert.That(results["9999999991"], Is.Null);
        Assert.That(results["9999999992"], Is.Null);
    }

    [Test]
    public async Task BulkLookup_MixesValidAndInvalidCodes_ReturnsErrorOnFirstInvalid()
    {
        var existingCode = CreateCode(1, "1234567890", "Test Product");
        _dbContext.FeacnCodes.Add(existingCode);
        await _dbContext.SaveChangesAsync();

        var request = new BulkFeacnCodeRequestDto(new[] 
        { 
            "1234567890", // valid existing
            "invalid",    // invalid format
            "0987654321"  // valid but non-existing
        });

        var result = await _controller.BulkLookup(request);
        
        // Should return 400 Bad Request due to invalid code
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

}
