// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
public class WordMatchTypesControllerTests
{
#pragma warning disable CS8618
    private AppDbContext _dbContext;
    private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ILogger<WordMatchTypesController> _logger;
    private WordMatchTypesController _controller;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"word_match_types_controller_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _logger = new LoggerFactory().CreateLogger<WordMatchTypesController>();
        _controller = new WordMatchTypesController(_mockHttpContextAccessor.Object, _dbContext, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task GetWordMatchTypes_ReturnsAllMatchTypes()
    {
        // Arrange
        _dbContext.WordMatchTypes.Add(new WordMatchType { Id = 1, Name = "Type1" });
        _dbContext.WordMatchTypes.Add(new WordMatchType { Id = 2, Name = "Type2" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetWordMatchTypes();

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(2));

        // Check DTO creation
        var dtos = result?.Value?.ToList() ?? [];
        Assert.That(dtos.Count, Is.EqualTo(2));
        Assert.That(dtos.Any(dto => dto.Id == 1 && dto.Name == "Type1"));
        Assert.That(dtos.Any(dto => dto.Id == 2 && dto.Name == "Type2"));
    }

    [Test]
    public async Task GetWordMatchTypes_ReturnsEmptyList_WhenNoMatchTypes()
    {
        // Act
        var result = await _controller.GetWordMatchTypes();

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetWordMatchTypes_ReturnsOrderedResults()
    {
        // Arrange
        _dbContext.WordMatchTypes.Add(new WordMatchType { Id = 3, Name = "Type3" });
        _dbContext.WordMatchTypes.Add(new WordMatchType { Id = 1, Name = "Type1" });
        _dbContext.WordMatchTypes.Add(new WordMatchType { Id = 2, Name = "Type2" });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.GetWordMatchTypes();

        // Assert
        Assert.That(result.Value, Is.Not.Null);
        var dtos = result.Value!.ToList();
        Assert.That(dtos.Count, Is.EqualTo(3));
        
        // Verify ordering by Id
        Assert.That(dtos[0].Id, Is.EqualTo(1));
        Assert.That(dtos[1].Id, Is.EqualTo(2));
        Assert.That(dtos[2].Id, Is.EqualTo(3));
    }
}