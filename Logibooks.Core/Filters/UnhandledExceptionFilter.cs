// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Logibooks.Core.RestModels;
using Logibooks.Core.Controllers;

namespace Logibooks.Core.Filters;

public class UnhandledExceptionFilter(ILogger<UnhandledExceptionFilter> logger) : IAsyncExceptionFilter
{
    private readonly ILogger _logger = logger;

    public Task OnExceptionAsync(ExceptionContext context)
    {
        var controller = context.RouteData.Values["controller"]?.ToString() ?? "unknown";
        var action = context.RouteData.Values["action"]?.ToString() ?? "unknown";

        _logger.LogError(context.Exception, "Unhandled exception in {Controller}:{Action}", controller, action);

        context.Result = LogibooksControllerBase._500(controller, action);

        context.ExceptionHandled = true;
        return Task.CompletedTask;
    }
}
