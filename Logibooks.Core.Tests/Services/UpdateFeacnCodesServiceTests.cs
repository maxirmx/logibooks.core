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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;
using Moq.Protected;
using NUnit.Framework;

using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.Services;

namespace Logibooks.Core.Tests.Services;

[TestFixture]
public class UpdateFeacnCodesServiceTests
{
    private AppDbContext _dbContext = null!;
    private Mock<ILogger<UpdateFeacnCodesService>> _mockLogger = null!;
    private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
    private IHttpClientFactory _httpClientFactory = null!;
    private UpdateFeacnCodesService _service = null!;

    [SetUp]
    public void Setup()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"test_db_{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new AppDbContext(options);

        // Setup mocks
        _mockLogger = new Mock<ILogger<UpdateFeacnCodesService>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        // Create real HttpClientFactory with mocked HttpMessageHandler
        var services = new ServiceCollection();
        services.AddHttpClient("", options => { }).ConfigurePrimaryHttpMessageHandler(() => _mockHttpMessageHandler.Object);
        var serviceProvider = services.BuildServiceProvider();
        _httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        _service = new UpdateFeacnCodesService(_dbContext, _mockLogger.Object, _httpClientFactory);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Dispose();
    }

    #region Helper Methods

    private void SetupHttpResponse(string url, string htmlContent)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(htmlContent, Encoding.UTF8, "text/html")
            });
    }

    private void SetupHttpException(string url, HttpRequestException exception)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);
    }

    private async Task<FeacnOrder> CreateTestOrder(int id, string title, string? url = null)
    {
        var order = new FeacnOrder
        {
            Id = id,
            Title = title,
            Url = url
        };
        _dbContext.FeacnOrders.Add(order);
        await _dbContext.SaveChangesAsync();
        return order;
    }

    private async Task<FeacnPrefix> CreateTestPrefix(int id, int orderId, string code, string? description = null, string? comment = null)
    {
        var prefix = new FeacnPrefix
        {
            Id = id,
            FeacnOrderId = orderId,
            Code = code,
            Description = description,
            Comment = comment
        };
        _dbContext.FeacnPrefixes.Add(prefix);
        await _dbContext.SaveChangesAsync();
        return prefix;
    }

    #endregion

    #region RunAsync Tests

    [Test]
    public async Task RunAsync_WithNoOrders_ReturnsEarly()
    {
        // Act
        await _service.RunAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No FEACN rows to process")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task RunAsync_WithValidOrders_ProcessesSuccessfully()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");
        
        var htmlContent = @"
            <table>
                <tr>
                    <td>1234</td>
                    <td>Test Product</td>
                    <td>Test Comment</td>
                </tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("1234"));
        Assert.That(prefixes[0].Description, Is.EqualTo("Test Product"));
        Assert.That(prefixes[0].Comment, Is.EqualTo("Test Comment"));
        Assert.That(prefixes[0].FeacnOrderId, Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_WithSpecialUrl_ProcessesWithSpecialLogic()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "10sr0318");
        
        var htmlContent = @"
            <table>
                <tr>
                    <td>Test Product</td>
                    <td>1234</td>
                    <td>Test Comment</td>
                </tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/10sr0318/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("1234")); // code is in second column for special URL
        Assert.That(prefixes[0].Description, Is.EqualTo("Test Product")); // name is in first column
        Assert.That(prefixes[0].Comment, Is.EqualTo("Test Comment"));
    }

    [Test]
    public async Task RunAsync_ReplacesExistingPrefixes()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");
        await CreateTestPrefix(100, 1, "OLD-CODE", "Old Description");

        var htmlContent = @"
            <table>
                <tr>
                    <td>NEW-CODE</td>
                    <td>New Description</td>
                </tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("NEW-CODE"));
        Assert.That(prefixes[0].Description, Is.EqualTo("New Description"));
    }

    [Test]
    public async Task RunAsync_WithMultipleOrders_ProcessesAll()
    {
        // Arrange
        await CreateTestOrder(1, "Order 1", "url1");
        await CreateTestOrder(2, "Order 2", "url2");

        var htmlContent1 = @"
            <table>
                <tr><td>7890</td><td>Product 1</td></tr>
            </table>";

        var htmlContent2 = @"
            <table>
                <tr><td>7891</td><td>Product 2</td></tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/url1/", htmlContent1);
        SetupHttpResponse("https://www.alta.ru/tamdoc/url2/", htmlContent2);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.OrderBy(p => p.Code).ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(2));
        Assert.That(prefixes[0].Code, Is.EqualTo("7890"));
        Assert.That(prefixes[0].FeacnOrderId, Is.EqualTo(1));
        Assert.That(prefixes[1].Code, Is.EqualTo("7891"));
        Assert.That(prefixes[1].FeacnOrderId, Is.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_WithHttpException_LogsErrorAndContinues()
    {
        // Arrange
        await CreateTestOrder(1, "Bad Order", "bad-url");
        await CreateTestOrder(2, "Good Order", "good-url");

        SetupHttpException("https://www.alta.ru/tamdoc/bad-url/", new HttpRequestException("Network error"));
        SetupHttpResponse("https://www.alta.ru/tamdoc/good-url/", @"
            <table>
                <tr><td>456789</td><td>Good Product</td></tr>
            </table>");

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("456789"));

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to download")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void RunAsync_WithCancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(() => _service.RunAsync(cts.Token));
    }

    #endregion

    #region HTML Parsing Tests

    [Test]
    public async Task RunAsync_SkipsRowsWithSkipPhrases()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");
        
        var htmlContent = @"
            <table>
                <tr><td>212143</td><td>Valid Product</td></tr>
                <tr><td>позиция исключена</td><td>12.12.2022ct</td></tr>
                <tr><td>(позиция введена</td><td>12.12.2022/td></tr>
                <tr><td>(введено постановлением правительства</td><td>Government Product</td></tr>
                <tr><td>наименование товара</td><td>Product Name Header</td></tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("212143"));
    }

    [Test]
    public async Task RunAsync_SkipsEmptyRows()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");
        
        var htmlContent = @"
            <table>
                <tr><td></td><td></td></tr>
                <tr><td>   </td><td>   </td></tr>
                <tr><td>212145</td><td>Valid Product</td></tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("212145"));
    }

    [Test]
    public async Task RunAsync_SkipsTablesWithWrongColumnCount()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");
        
        var htmlContent = @"
            <table>
                <tr><td>SINGLE-COLUMN</td></tr>
            </table>
            <table>
                <tr><td>COL1</td><td>COL2</td><td>COL3</td><td>COL4</td></tr>
            </table>
            <table>
                <tr><td>212146</td><td>Valid Product</td></tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("212146"));
    }


    [Test]
    public async Task RunAsync_HandlesTablesWithoutRows()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");
        
        var htmlContent = @"
            <table></table>
            <table>
                <tr><td>3210</td><td>Valid Product</td></tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("3210"));
    }

    [Test]
    public async Task RunAsync_HandlesOrdersWithNullUrl()
    {
        // Arrange
        await CreateTestOrder(1, "Order with URL", "test-url");
        await CreateTestOrder(2, "Order without URL", null);

        var htmlContent = @"
            <table>
                <tr><td>3211</td><td>Valid Product</td></tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("3211"));
        Assert.That(prefixes[0].FeacnOrderId, Is.EqualTo(1));
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task RunAsync_WithThreeColumnTable_ProcessesCorrectly()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");
        
        var htmlContent = @"
            <table>
                <tr><td>9123</td><td>Product Name</td><td>Comment Text</td></tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("9123"));
        Assert.That(prefixes[0].Description, Is.EqualTo("Product Name"));
        Assert.That(prefixes[0].Comment, Is.EqualTo("Comment Text"));
    }

    [Test]
    public async Task RunAsync_WithTwoColumnTable_LeavesCommentEmpty()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");
        
        var htmlContent = @"
            <table>
                <tr><td>8123</td><td>Product Name</td></tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(1));
        Assert.That(prefixes[0].Code, Is.EqualTo("8123"));
        Assert.That(prefixes[0].Description, Is.EqualTo("Product Name"));
        Assert.That(prefixes[0].Comment, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task RunAsync_WithNoTables_ProcessesWithoutError()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");
        
        var htmlContent = @"<div>No tables here</div>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes.ToListAsync();
        Assert.That(prefixes.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task RunAsync_ParsesExceptionsAndMultipleCodes()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");

        var htmlContent = @"
            <table>
                <tr><td>12 34, 56 78 (кроме 1234 90 12, 567834 56)</td><td>Product</td></tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        var prefixes = await _dbContext.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .OrderBy(p => p.Code)
            .ToListAsync();

        Assert.That(prefixes.Count, Is.EqualTo(2));
        Assert.That(prefixes[0].Code, Is.EqualTo("1234"));
        Assert.That(prefixes[1].Code, Is.EqualTo("5678"));

        foreach (var prefix in prefixes)
        {
            var exc = prefix.FeacnPrefixExceptions.Select(e => e.Code).OrderBy(c => c).ToList();
            Assert.That(exc, Is.EquivalentTo(["12349012", "56783456"]));
        }
    }

    [Test]
    public async Task RunAsync_LogsProgressInformation()
    {
        // Arrange
        await CreateTestOrder(1, "Test Order", "test-url");
        
        var htmlContent = @"
            <table>
                <tr><td>7123</td><td>Product Name</td></tr>
            </table>";

        SetupHttpResponse("https://www.alta.ru/tamdoc/test-url/", htmlContent);

        // Act
        await _service.RunAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Downloading FEACN tables")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Extracted 1 FEACN rows")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Adding 1 new FEACN prefixes")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
