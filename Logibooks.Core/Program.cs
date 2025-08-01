﻿// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
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

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.Extensions;
using Logibooks.Core.Settings;
using Logibooks.Core.Services;
using Logibooks.Core.Filters;
using Quartz;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var certPath = config["Kestrel:Certificates:Default:Path"];
var certPassword = config["Kestrel:Certificates:Default:Password"];
bool useHttps = !string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword) && File.Exists(certPath);
builder.WebHost.ConfigureKestrel(options =>
{
    if (useHttps)
    {
        options.ListenAnyIP(8081, listenOptions => listenOptions.UseHttps(certPath!, certPassword));
    }
    options.ListenAnyIP(8080);
});

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<OrderMappingProfile>());

builder.Services
    .Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"))
    .AddScoped<IJwtUtils, JwtUtils>()
    .AddScoped<IUpdateCountriesService, UpdateCountriesService>()
    .AddScoped<IUpdateFeacnCodesService, UpdateFeacnCodesService>()
    .AddScoped<IFeacnPrefixCheckService, FeacnPrefixCheckService>()
    .AddScoped<IOrderValidationService, OrderValidationService>()
    .AddScoped<IRegisterValidationService, RegisterValidationService>()
    .AddScoped<IRegisterProcessingService, RegisterProcessingService>()
    .AddScoped<IIndPostXmlService, IndPostXmlService>()
    .AddScoped<IOrderIndPostGenerator, OrderIndPostGenerator>()
    .AddScoped<IUserInformationService, UserInformationService>()
    .AddSingleton<IMorphologySearchService, MorphologySearchService>()
    .AddScoped<UnhandledExceptionFilter>()
    .AddHttpContextAccessor()
    .AddControllers(options => options.Filters.Add<UnhandledExceptionFilter>());

builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddQuartz(q =>
{
    var updateCountriesJobKey = new JobKey("UpdateCountries");
    q.AddJob<UpdateCountriesJob>(opts => opts.WithIdentity(updateCountriesJobKey));

    var updateCountriesCron = config["Jobs:UpdateCountries"];
    if (!string.IsNullOrWhiteSpace(updateCountriesCron))
    {
        q.AddTrigger(opts => opts
            .ForJob(updateCountriesJobKey)
            .WithIdentity("UpdateCountries-trigger")
            .WithCronSchedule(updateCountriesCron));
    }

    var updateFeacnCodesKey = new JobKey("UpdateFeacnCodes");
    q.AddJob<UpdateFeacnCodesJob>(opts => opts.WithIdentity(updateFeacnCodesKey));

    var updateFeacnCodesCron = config["Jobs:UpdateFeacnCodes"];
    if (!string.IsNullOrWhiteSpace(updateFeacnCodesCron))
    {
        q.AddTrigger(opts => opts
            .ForJob(updateFeacnCodesKey)
            .WithIdentity("UpdateFeacnCodes-trigger")
            .WithCronSchedule(updateFeacnCodesCron));
    }
});
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Logibooks Core Api", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization token. Example: \"Authorization: {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    var scm = new OpenApiSecurityScheme
    {
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { scm, Array.Empty<string>() } });
});

var app = builder.Build();

// Apply pending migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app
    .UseMiddleware<JwtMiddleware>()
    .UseSwagger()
    .UseSwaggerUI();
if (useHttps)
{
    app.UseHttpsRedirection();
}

app
    .UseCors()
    .UseAuthorization();

app.MapControllers();
app.Run();
