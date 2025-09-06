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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;
using Moq;

using Logibooks.Core.Controllers;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Tests.Controllers.Registers;

[TestFixture]
public class RegistersControllerValidationTests : RegistersControllerTestsBase
{
    [Test]
    public async Task ValidateSw_RunsService_ForLogist()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.Add(new Register { Id = 5, FileName = "r.xlsx", TheOtherCompanyId = 3 });
        await _dbContext.SaveChangesAsync();

        var handle = Guid.NewGuid();
        _mockRegValidationService.Setup(s => s.StartSwValidationAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(handle);

        var result = await _controller.ValidateRegisterSw(5);

        _mockRegValidationService.Verify(s => s.StartSwValidationAsync(5, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(((GuidReference)ok!.Value!).Id, Is.EqualTo(handle));
    }

    [Test]
    public async Task ValidateSw_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.ValidateRegisterSw(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockRegValidationService.Verify(s => s.StartSwValidationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ValidateSw_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.ValidateRegisterSw(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task ValidateSw_ReturnsConflict_WhenAlreadyValidating()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.Add(new Register { Id = 7, FileName = "r.xlsx", TheOtherCompanyId = 3 });
        await _dbContext.SaveChangesAsync();

        _mockRegValidationService.Setup(s => s.StartSwValidationAsync(7, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        var result = await _controller.ValidateRegisterSw(7);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task ValidateRegisterFeacn_RunsService_ForLogist()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.Add(new Register { Id = 6, FileName = "r.xlsx", TheOtherCompanyId = 3 });
        await _dbContext.SaveChangesAsync();

        var handle = Guid.NewGuid();
        _mockRegValidationService.Setup(s => s.StartFeacnValidationAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(handle);

        var result = await _controller.ValidateRegisterFeacn(6);

        _mockRegValidationService.Verify(s => s.StartFeacnValidationAsync(6, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(((GuidReference)ok!.Value!).Id, Is.EqualTo(handle));
    }

    [Test]
    public async Task ValidateRegisterFeacn_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.ValidateRegisterFeacn(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockRegValidationService.Verify(s => s.StartFeacnValidationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ValidateRegisterFeacn_ReturnsNotFound_WhenMissing()
    {
        SetCurrentUserId(1);
        var result = await _controller.ValidateRegisterFeacn(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task ValidateRegisterFeacn_ReturnsConflict_WhenAlreadyValidating()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.Add(new Register { Id = 8, FileName = "r.xlsx", TheOtherCompanyId = 3 });
        await _dbContext.SaveChangesAsync();

        _mockRegValidationService.Setup(s => s.StartFeacnValidationAsync(8, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        var result = await _controller.ValidateRegisterFeacn(8);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
    }

    [Test]
    public async Task GetValidationProgress_ReturnsData()
    {
        SetCurrentUserId(1);
        var progress = new ValidationProgress { HandleId = Guid.NewGuid(), Total = 10, Processed = 5 };
        _mockRegValidationService.Setup(s => s.GetProgress(progress.HandleId)).Returns(progress);

        var result = await _controller.GetValidationProgress(progress.HandleId);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.EqualTo(progress));
    }

    [Test]
    public async Task GetValidationProgress_ReturnsNotFound()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetValidationProgress(Guid.NewGuid());

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetValidationProgress_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2); // Admin user, not logist
        var handle = Guid.NewGuid();
        var result = await _controller.GetValidationProgress(handle);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockRegValidationService.Verify(s => s.GetProgress(It.IsAny<Guid>()), Times.Never);
    }

    [Test]
    public async Task CancelValidation_ReturnsNoContent()
    {
        SetCurrentUserId(1);
        var handle = Guid.NewGuid();
        _mockRegValidationService.Setup(s => s.Cancel(handle)).Returns(true);

        var result = await _controller.CancelValidation(handle);

        Assert.That(result, Is.TypeOf<NoContentResult>());
    }

    [Test]
    public async Task CancelValidation_ReturnsNotFound()
    {
        SetCurrentUserId(1);
        var result = await _controller.CancelValidation(Guid.NewGuid());

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task CancelValidation_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2); // Admin user, not logist
        var handle = Guid.NewGuid();
        var result = await _controller.CancelValidation(handle);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        _mockRegValidationService.Verify(s => s.Cancel(It.IsAny<Guid>()), Times.Never);
    }

    [Test]
    public async Task ValidateRegisterFeacn_WithRealService_CreatesFeacnLinks()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 200, FileName = "r.xlsx", TheOtherCompanyId = 3 };
        var feacnOrder = new FeacnOrder { Id = 300, Title = "t", Enabled = true };
        var prefix = new FeacnPrefix { Id = 400, Code = "12", FeacnOrderId = 300, FeacnOrder = feacnOrder };
        var feacnCode = new FeacnCode { Id = 1, Code = "1203000000", CodeEx = "", Name = "Копра", NormalizedName = "копра" };
        var parcel = new WbrParcel { Id = 201, RegisterId = 200, StatusId = 1, TnVed = "1203000000" };  // "Копра"
        _dbContext.Registers.Add(register);
        _dbContext.FeacnOrders.Add(feacnOrder);
        _dbContext.FeacnPrefixes.Add(prefix);
        _dbContext.FeacnCodes.Add(feacnCode);
        _dbContext.Parcels.Add(parcel);
        await _dbContext.SaveChangesAsync();

        var orderValidationService = new ParcelValidationService(_dbContext, new MorphologySearchService(), new FeacnPrefixCheckService(_dbContext));
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(x => x.GetService(typeof(AppDbContext))).Returns(_dbContext);
        spMock.Setup(x => x.GetService(typeof(IParcelValidationService))).Returns(orderValidationService);
        spMock.Setup(x => x.GetService(typeof(IFeacnPrefixCheckService))).Returns(new FeacnPrefixCheckService(_dbContext));
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        // Update the logger type to match the expected type for RegisterValidationService
        var realRegSvc = new RegisterValidationService(_dbContext, scopeFactoryMock.Object, new LoggerFactory().CreateLogger<RegisterValidationService>(), new MorphologySearchService(), new FeacnPrefixCheckService(_dbContext));
        _controller = new RegistersController(_mockHttpContextAccessor.Object, _dbContext, _userService, _logger, realRegSvc, _mockRegFeacnLookupService.Object, _mockProcessingService.Object, _mockIndPostGenerator.Object);

        var result = await _controller.ValidateRegisterFeacn(200);
        var handle = ((GuidReference)((OkObjectResult)result.Result!).Value!).Id;

        // wait for completion
        ValidationProgress? progress = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(2))
        {
            progress = realRegSvc.GetProgress(handle);
            if (progress != null && progress.Finished)
                break;
            await Task.Delay(50);
        }

        var parcelReloaded = await _dbContext.Parcels.Include(o => o.BaseParcelFeacnPrefixes).FirstAsync(o => o.Id == 201);
        Assert.That(parcelReloaded.BaseParcelFeacnPrefixes.Any(l => l.FeacnPrefixId == 400), Is.True);
    }
}
