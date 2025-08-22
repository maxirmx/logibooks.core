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

using System;
using System.Linq;
using System.Threading.Tasks;
using Logibooks.Core.Models;

using NUnit.Framework;

using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Controllers.Registers;

[TestFixture]
public class RegistersControllerCrudTests : RegistersControllerTestsBase
{
    // --- GET REGISTERS ---
    [Test]
    public async Task GetRegisters_ReturnsData_ForLogist()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters();
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var ok = result.Result as OkObjectResult;
        Assert.That(ok!.Value, Is.InstanceOf<PagedResult<RegisterViewItem>>());
    }


    [Test]
    public async Task GetRegisters_ReturnsZeroOrders_WhenNoOrders()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegisters();
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.OrderBy(i => i.Id).ToArray();

        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(items[0].OrdersTotal, Is.EqualTo(0));
        Assert.That(items[0].OrdersByCheckStatus.Count, Is.EqualTo(0));
        Assert.That(items[1].OrdersTotal, Is.EqualTo(0));
        Assert.That(items[1].OrdersByCheckStatus.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetRegisters_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var result = await _controller.GetRegisters();
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    // --- GET REGISTER BY ID ---
    [Test]
    public async Task GetRegister_ReturnsRegister_ForLogist()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, FileName = "reg.xlsx", CompanyId = 2, TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegister(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Id, Is.EqualTo(1));
        Assert.That(result.Value.FileName, Is.EqualTo("reg.xlsx"));
        Assert.That(result.Value.OrdersTotal, Is.EqualTo(0));
        Assert.That(result.Value.OrdersByCheckStatus.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetRegister_ReturnsOrderCounts()
    {
        SetCurrentUserId(1);
        // Use real seeded check statuses
        _dbContext.CheckStatuses.AddRange(
            new ParcelCheckStatus { Id = 1, Title = "Loaded" },
            new ParcelCheckStatus { Id = 2, Title = "Processed" });

        var register = new Register { Id = 1, FileName = "reg.xlsx", CompanyId = 2, TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 1 },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 2, CheckStatusId = 2 },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 1, CheckStatusId = 1 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegister(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.OrdersTotal, Is.EqualTo(3));
        Assert.That(result.Value.OrdersByCheckStatus[1], Is.EqualTo(2));
        Assert.That(result.Value.OrdersByCheckStatus[2], Is.EqualTo(1));
    }

    [Test]
    public async Task GetRegister_ReturnsOrderCounts_WithMultipleStatusGroups()
    {
        SetCurrentUserId(1);
        _dbContext.CheckStatuses.AddRange(
            new ParcelCheckStatus { Id = 1,  Title = "Loaded" },
            new ParcelCheckStatus { Id = 2,  Title = "Processed" },
            new ParcelCheckStatus { Id = 3,  Title = "Delivered" }
        );
        var register = new Register { Id = 1, FileName = "reg.xlsx", CompanyId = 2, TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(
            new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1, CheckStatusId = 1 },
            new WbrParcel { Id = 2, RegisterId = 1, StatusId = 2, CheckStatusId = 2 },
            new WbrParcel { Id = 3, RegisterId = 1, StatusId = 3, CheckStatusId = 3 },
            new WbrParcel { Id = 4, RegisterId = 1, StatusId = 3, CheckStatusId = 3 }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegister(1);

        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.OrdersTotal, Is.EqualTo(4));
        Assert.That(result.Value.OrdersByCheckStatus[1], Is.EqualTo(1));
        Assert.That(result.Value.OrdersByCheckStatus[2], Is.EqualTo(1));
        Assert.That(result.Value.OrdersByCheckStatus[3], Is.EqualTo(2));
    }

    [Test]
    public async Task GetRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        _dbContext.Registers.Add(new Register { Id = 1, FileName = "reg.xlsx", CompanyId = 2, TheOtherCompanyId = 3 });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetRegister(1);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task GetRegister_ReturnsNotFound_WhenRegisterMissing()
    {
        SetCurrentUserId(1);

        var result = await _controller.GetRegister(99);

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    // --- PUT REGISTER ---
    [Test]
    public async Task UpdateRegister_UpdatesData_WhenUserIsLogist()
    {
        SetCurrentUserId(1);
        _dbContext.Countries.Add(new Country { IsoNumeric = 100, IsoAlpha2 = "XX", NameRuShort = "XX" });
        var register = new Register { Id = 1, FileName = "r.xlsx", TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var update = new RegisterUpdateItem
        {
            InvoiceNumber = "INV",
            InvoiceDate = new DateOnly(2025, 1, 2),
            TheOtherCountryCode = 100,
            TransportationTypeId = 1,
            CustomsProcedureId = 1
        };

        var result = await _controller.UpdateRegister(1, update);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var saved = await _dbContext.Registers.FindAsync(1);
        Assert.That(saved!.InvoiceNumber, Is.EqualTo("INV"));
        Assert.That(saved.InvoiceDate, Is.EqualTo(new DateOnly(2025, 1, 2)));
        Assert.That(saved.TheOtherCountryCode, Is.EqualTo((short)100));
        Assert.That(saved.TransportationTypeId, Is.EqualTo(1));
        Assert.That(saved.CustomsProcedureId, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateRegister_UpdatesAllFields_IncludingNullable()
    {
        SetCurrentUserId(1);
        var register = new Register
        {
            Id = 1,
            FileName = "r.xlsx",
            DealNumber = "OLDDEAL",
            TheOtherCompanyId = 3,
            InvoiceNumber = "OLDINV",
            InvoiceDate = new DateOnly(2024, 1, 1),
            TheOtherCountryCode = 860, 
            TransportationTypeId = 1, 
            CustomsProcedureId = 1 
        };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var update = new RegisterUpdateItem
        {
            DealNumber = "NEWDEAL",
            TheOtherCompanyId = 2, 
            InvoiceNumber = "NEWINV",
            InvoiceDate = new DateOnly(2025, 2, 3),
            TheOtherCountryCode = 643, 
            TransportationTypeId = 2, 
            CustomsProcedureId = 2 
        };

        var result = await _controller.UpdateRegister(1, update);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var saved = await _dbContext.Registers.FindAsync(1);
        Assert.That(saved!.DealNumber, Is.EqualTo("NEWDEAL"));
        Assert.That(saved.TheOtherCompanyId, Is.EqualTo(2));
        Assert.That(saved.InvoiceNumber, Is.EqualTo("NEWINV"));
        Assert.That(saved.InvoiceDate, Is.EqualTo(new DateOnly(2025, 2, 3)));
        Assert.That(saved.TheOtherCountryCode, Is.EqualTo((short)643));
        Assert.That(saved.TransportationTypeId, Is.EqualTo(2));
        Assert.That(saved.CustomsProcedureId, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateRegister_UpdatesNullableFieldsToNull()
    {
        SetCurrentUserId(1);
        var register = new Register
        {
            Id = 1,
            FileName = "r.xlsx",
            DealNumber = "DEAL",
            TheOtherCompanyId = 2,
            InvoiceNumber = "INV",
            InvoiceDate = new DateOnly(2025, 1, 2),
            TheOtherCountryCode = 643,
            TransportationTypeId = 1,
            CustomsProcedureId = 1
        };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var update = new RegisterUpdateItem
        {
            DealNumber = null,
            TheOtherCompanyId = 0, // Should set to null
            InvoiceNumber = null,
            InvoiceDate = null,
            TheOtherCountryCode = 0, // Should set to null
            TransportationTypeId = 1,
            CustomsProcedureId = 1
        };

        var result = await _controller.UpdateRegister(1, update);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        var saved = await _dbContext.Registers.FindAsync(1);
        Assert.That(saved!.DealNumber, Is.EqualTo("DEAL")); // Not updated
        Assert.That(saved.TheOtherCompanyId, Is.Null);
        Assert.That(saved.InvoiceNumber, Is.EqualTo("INV")); // Not updated
        Assert.That(saved.InvoiceDate, Is.EqualTo(new DateOnly(2025, 1, 2))); // Not updated
        Assert.That(saved.TheOtherCountryCode, Is.Null);
    }

    [Test]
    public async Task UpdateRegister_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(2);
        var register = new Register { Id = 1, FileName = "r.xlsx", TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.UpdateRegister(1, new RegisterUpdateItem());

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task UpdateRegister_ReturnsNotFound_WhenRegisterMissing()
    {
        SetCurrentUserId(1);

        var result = await _controller.UpdateRegister(99, new RegisterUpdateItem());

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateRegister_ReturnsNotFound_WhenInvalidTheOtherCountryCode()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, FileName = "r.xlsx", TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var update = new RegisterUpdateItem { TheOtherCountryCode = 9999 }; // Invalid country code

        var result = await _controller.UpdateRegister(1, update);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateRegister_ReturnsNotFound_WhenInvalidTransportationTypeId()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, FileName = "r.xlsx", TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var update = new RegisterUpdateItem { TransportationTypeId = 9999 }; // Invalid transportation type ID

        var result = await _controller.UpdateRegister(1, update);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateRegister_ReturnsNotFound_WhenInvalidCustomsProcedureId()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, FileName = "r.xlsx", TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var update = new RegisterUpdateItem { CustomsProcedureId = 9999 }; // Invalid customs procedure ID

        var result = await _controller.UpdateRegister(1, update);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UpdateRegister_ReturnsNotFound_WhenInvalidTheOtherCompanyId()
    {
        SetCurrentUserId(1);
        var register = new Register { Id = 1, FileName = "r.xlsx", TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var update = new RegisterUpdateItem { TheOtherCompanyId = 9999 }; // Invalid company ID

        var result = await _controller.UpdateRegister(1, update);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    // --- DELETE REGISTER ---
    [Test]
    public async Task DeleteRegister_DeletesEmptyRegister_WhenUserIsLogist()
    {
        SetCurrentUserId(1); // Logist user

        var register = new Register { Id = 1, FileName = "reg.xlsx", TheOtherCompanyId = 3 };

        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteRegister(1);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(await _dbContext.Registers.FindAsync(1), Is.Null);
        Assert.That(_dbContext.Orders.Any(o => o.RegisterId == 1), Is.False);
    }

    [Test]
    public async Task DeleteRegister_FailToDeleteRegisterAndOrders_WhenUserIsLogist()
    {
        SetCurrentUserId(1); // Logist user

        var register = new Register { Id = 1, FileName = "reg.xlsx", TheOtherCompanyId = 3 };
        var order1 = new WbrParcel { Id = 1, RegisterId = 1, StatusId = 1 };
        var order2 = new WbrParcel { Id = 2, RegisterId = 1, StatusId = 1 };
        _dbContext.Registers.Add(register);
        _dbContext.Orders.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteRegister(1);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(await _dbContext.Registers.FindAsync(1), Is.Null);
        Assert.That(_dbContext.Orders.Any(o => o.RegisterId == 1), Is.False);
    }

    [Test]
    public async Task DeleteRegister_ReturnsForbidden_WhenUserIsNotLogist()
    {
        SetCurrentUserId(2); // Non-logist user

        var register = new Register { Id = 1, FileName = "reg.xlsx", TheOtherCompanyId = 3 };
        _dbContext.Registers.Add(register);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.DeleteRegister(1);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task DeleteRegister_ReturnsNotFound_WhenRegisterDoesNotExist()
    {
        SetCurrentUserId(1); // Logist user

        var result = await _controller.DeleteRegister(999);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task SetOrderStatuses_ReturnsForbidden_ForNonLogist()
    {
        SetCurrentUserId(99); // Not a logist
        var register = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        _dbContext.SaveChanges();
        var status = new ParcelStatus { Id = 1, Title = "TestStatus" };
        _dbContext.Statuses.Add(status);
        _dbContext.SaveChanges();
        var result = await _controller.SetParcelStatuses(1, 1);
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task SetOrderStatuses_ReturnsNotFound_WhenRegisterMissing()
    {
        SetCurrentUserId(1); // Logist
        var status = new ParcelStatus { Id = 1, Title = "TestStatus" };
        _dbContext.Statuses.Add(status);
        _dbContext.SaveChanges();
        var result = await _controller.SetParcelStatuses(999, 1); // Register does not exist
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task SetOrderStatuses_ReturnsNotFound_WhenStatusMissing()
    {
        SetCurrentUserId(1); // Logist
        var register = new Register { Id = 1, FileName = "r.xlsx", CompanyId = 2 };
        _dbContext.Registers.Add(register);
        _dbContext.SaveChanges();
        var result = await _controller.SetParcelStatuses(1, 999); // Status does not exist
        Assert.That(result, Is.TypeOf<ObjectResult>());
        var obj = result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }
}
