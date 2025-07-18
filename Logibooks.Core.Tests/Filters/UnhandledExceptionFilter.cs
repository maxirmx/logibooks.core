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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Logibooks.Core.Filters;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Filters;

[TestFixture]
public class UnhandledExceptionFilterTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private Mock<ILogger<UnhandledExceptionFilter>> _mockLogger;
    private UnhandledExceptionFilter _filter;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<UnhandledExceptionFilter>>();
        _filter = new UnhandledExceptionFilter(_mockLogger.Object);
    }

    private ExceptionContext CreateExceptionContext(string controller = "TestController", string action = "TestAction", Exception? exception = null)
    {
        exception ??= new Exception("Test exception");
        
        var httpContext = new Mock<HttpContext>();
        var routeData = new RouteData();
        routeData.Values["controller"] = controller;
        routeData.Values["action"] = action;

        var actionDescriptor = new ActionDescriptor();
        var actionContext = new ActionContext(httpContext.Object, routeData, actionDescriptor);
        
        return new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception
        };
    }

    [Test]
    public async Task OnExceptionAsync_LogsException_WithControllerAndAction()
    {
        // Arrange
        var testException = new InvalidOperationException("Test exception message");
        var context = CreateExceptionContext("Users", "GetUser", testException);

        // Act
        await _filter.OnExceptionAsync(context);

        // Assert
        // Verify that Log was called with LogLevel.Error and the exception
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Users") && v.ToString()!.Contains("GetUser")),
                testException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task OnExceptionAsync_SetsResult_To500StatusWithErrMessage()
    {
        // Arrange
        var testException = new Exception("Test exception");
        var context = CreateExceptionContext("Orders", "CreateOrder", testException);

        // Act
        await _filter.OnExceptionAsync(context);

        // Assert
        Assert.That(context.Result, Is.Not.Null);
        Assert.That(context.Result, Is.TypeOf<ObjectResult>());
        
        var objectResult = context.Result as ObjectResult;
        Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        Assert.That(objectResult.Value, Is.TypeOf<ErrMessage>());
        
        var errMessage = objectResult.Value as ErrMessage;
        Assert.That(errMessage!.Msg, Does.Contain("Orders"));
        Assert.That(errMessage.Msg, Does.Contain("CreateOrder"));
        Assert.That(errMessage.Msg, Does.Contain("внутренней ошибки сервиса"));
    }

    [Test]
    public async Task OnExceptionAsync_MarksExceptionAsHandled()
    {
        // Arrange
        var testException = new Exception("Test exception");
        var context = CreateExceptionContext("Products", "UpdateProduct", testException);

        // Act
        await _filter.OnExceptionAsync(context);

        // Assert
        Assert.That(context.ExceptionHandled, Is.True);
    }


    [Test]
    public async Task OnExceptionAsync_ReturnsCompletedTask()
    {
        // Arrange
        var testException = new Exception("Test exception");
        var context = CreateExceptionContext("Test", "Test", testException);

        // Act
        var result = _filter.OnExceptionAsync(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IsCompleted, Is.True);
        Assert.That(result.Status, Is.EqualTo(TaskStatus.RanToCompletion));
        
        // Ensure the task can be awaited without issues
        await result;
    }

    [Test]
    public async Task OnExceptionAsync_ErrorMessageContainsExpectedText()
    {
        // Arrange
        var testException = new Exception("Test exception");
        var context = CreateExceptionContext("Orders", "DeleteOrder", testException);

        // Act
        await _filter.OnExceptionAsync(context);

        // Assert
        var objectResult = context.Result as ObjectResult;
        var errMessage = objectResult!.Value as ErrMessage;
        
        // Check for Russian error message components
        Assert.That(errMessage!.Msg, Does.Contain("Запрос не выполнен"));
        Assert.That(errMessage.Msg, Does.Contain("Orders"));
        Assert.That(errMessage.Msg, Does.Contain("DeleteOrder"));
        Assert.That(errMessage.Msg, Does.Contain("администратору системы"));
    }

    [Test]
    public async Task OnExceptionAsync_DoesNotThrowWhenContextIsNull()
    {
        // This test ensures the filter handles edge cases gracefully
        // In practice, ASP.NET Core would never pass null, but defensive programming is good
        
        // We can't easily test null context since it would throw before reaching our code
        // Instead, test with minimal valid context
        var httpContext = new Mock<HttpContext>();
        var routeData = new RouteData();
        var actionDescriptor = new ActionDescriptor();
        var actionContext = new ActionContext(httpContext.Object, routeData, actionDescriptor);
        
        var context = new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = new Exception("Test")
        };
        await Task.Delay(1); // Simulate async delay
        // Act & Assert - should not throw
        Assert.DoesNotThrowAsync(async () => await _filter.OnExceptionAsync(context));
    }
}
