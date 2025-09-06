// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Logibooks.Core.Authorization;
using Logibooks.Core.RestModels;


[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // skip authorization if action is decorated with [AllowAnonymous] attribute
        var allowAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any();
        if (allowAnonymous)
            return;

        // authorization
        context.HttpContext.Items.TryGetValue("UserId", out var userIdObj);
        int? userId = userIdObj is int id ? id : null;
        if (userId == null)
        {
            const string errorMessage = "Необходимо войти в систему.";
            var logger = context.HttpContext.RequestServices.GetService(typeof(ILogger<AuthorizeAttribute>)) as ILogger<AuthorizeAttribute>;
            logger?.LogWarning(errorMessage);
            context.Result = new JsonResult(new ErrMessage { Msg = errorMessage }) { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }
    }
}
