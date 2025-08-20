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
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Logibooks.Core.Services;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class FeacnListProcessingServiceTests
{
    private AppDbContext _dbContext = null!;
    private FeacnListProcessingService _service = null!;
    private ILogger<FeacnListProcessingService> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _logger = new LoggerFactory().CreateLogger<FeacnListProcessingService>();
        _service = new FeacnListProcessingService(_dbContext, _logger);

        // Ensure database is created
        _dbContext.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public async Task ProcessFeacnCodesFromExcelAsync_WithRealFile_ShouldParseCorrectly()
    {
        // Skip this test if the file doesn't exist in test environment
        var testFilePath = "/tmp/tnved.xlsx";
        if (!File.Exists(testFilePath))
        {
            Assert.Ignore("Test Excel file not available");
            return;
        }

        // Arrange
        var excelBytes = await File.ReadAllBytesAsync(testFilePath);

        // Act
        var result = await _service.ProcessFeacnCodesFromExcelAsync(excelBytes, "tnved.xlsx");

        // Assert
        Assert.That(result, Is.GreaterThan(0));
        
        // Verify data was actually saved (allow for small differences due to processing logic)
        var savedCodes = await _dbContext.FeacnCodes.CountAsync();
        Assert.That(savedCodes, Is.GreaterThan(0));
        Assert.That(Math.Abs(savedCodes - result), Is.LessThan(100)); // Allow small variance
    }

    [Test]
    public void ProcessFeacnCodesFromExcelAsync_WithEmptyContent_ShouldThrowException()
    {
        // Arrange
        var emptyBytes = Array.Empty<byte>();

        // Act & Assert - ExcelDataReader throws HeaderException for invalid file format
        var ex = Assert.ThrowsAsync<ExcelDataReader.Exceptions.HeaderException>(async () =>
            await _service.ProcessFeacnCodesFromExcelAsync(emptyBytes, "empty.xlsx"));
        
        Assert.That(ex!.Message, Does.Contain("Invalid file signature"));
    }

    [Test]
    public void ProcessFeacnCodesFromExcelAsync_WithNullFileName_ShouldNotThrow()
    {
        // Arrange  
        var testFilePath = "/tmp/tnved.xlsx";
        if (!File.Exists(testFilePath))
        {
            Assert.Ignore("Test Excel file not available");
            return;
        }

        var testBytes = File.ReadAllBytes(testFilePath);

        // Act & Assert
        Assert.DoesNotThrowAsync(async () =>
            await _service.ProcessFeacnCodesFromExcelAsync(testBytes, ""));
    }

    [Test]
    public void ServiceInstantiation_ShouldCreateSuccessfully()
    {
        // Act & Assert
        Assert.That(_service, Is.Not.Null);
        Assert.That(_dbContext, Is.Not.Null);
        Assert.That(_logger, Is.Not.Null);
    }
}