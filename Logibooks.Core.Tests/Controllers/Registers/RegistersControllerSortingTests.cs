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
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE
// OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System;
using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Controllers.Registers;

[TestFixture]
public class RegistersControllerSortingTests : RegistersControllerTestsBase
{
    // Sorting by FileName descending
    [Test]
    public async Task GetRegisters_SortsByFileName_Descending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "a.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "b.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "fileName", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].FileName, Is.EqualTo("b.xlsx"));
        Assert.That(items[1].FileName, Is.EqualTo("a.xlsx"));
    }

    // Sorting by Date descending
    [Test]
    public async Task GetRegisters_SortsByDate_Descending()
    {
        SetCurrentUserId(1);
        var earlierDate = DateTime.UtcNow.AddDays(-1);
        var laterDate = DateTime.UtcNow;
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, DTime = earlierDate },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, DTime = laterDate }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "date", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].Id, Is.EqualTo(2)); // Later date comes first in desc order
        Assert.That(items[1].Id, Is.EqualTo(1)); // Earlier date comes second
    }

    // Sorting by DealNumber ascending
    [Test]
    public async Task GetRegisters_SortsByDealNumber_Ascending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, DealNumber = "DEAL-B" },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, DealNumber = "DEAL-A" }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "dealNumber", sortOrder: "asc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].DealNumber, Is.EqualTo("DEAL-A"));
        Assert.That(items[1].DealNumber, Is.EqualTo("DEAL-B"));
    }

    // Sorting by DealNumber descending
    [Test]
    public async Task GetRegisters_SortsByDealNumber_Descending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, DealNumber = "DEAL-A" },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, DealNumber = "DEAL-B" }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "dealNumber", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].DealNumber, Is.EqualTo("DEAL-B"));
        Assert.That(items[1].DealNumber, Is.EqualTo("DEAL-A"));
    }

    // Sorting by OrdersTotal across pages
    [Test]
    public async Task GetRegisters_SortsByOrdersTotalAcrossPages()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx" , CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "r2.xlsx" , CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 3, FileName = "r3.xlsx" , CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 4, FileName = "r4.xlsx" , CompanyId = 2, TheOtherCompanyId = 3 }
        );
        _dbContext.Orders.AddRange(
            new WbrOrder { RegisterId = 1, StatusId = 1 },
            new WbrOrder { RegisterId = 2, StatusId = 1 },
            new WbrOrder { RegisterId = 2, StatusId = 1 },
            new WbrOrder { RegisterId = 3, StatusId = 1 },
            new WbrOrder { RegisterId = 3, StatusId = 1 },
            new WbrOrder { RegisterId = 3, StatusId = 1 }
        );
        await _dbContext.SaveChangesAsync();
        var r1 = await _controller.GetRegisters(page: 1, pageSize: 2, sortBy: "ordersTotal", sortOrder: "desc");
        var ok1 = r1.Result as OkObjectResult;
        var pr1 = ok1!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr1!.Items.First().Id, Is.EqualTo(3));
        var r2 = await _controller.GetRegisters(page: 2, pageSize: 2, sortBy: "ordersTotal", sortOrder: "desc");
        var ok2 = r2.Result as OkObjectResult;
        var pr2 = ok2!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr2!.Items.First().Id, Is.EqualTo(1));
    }

    // Sorting by TransportationType.Name descending
    [Test]
    public async Task GetRegisters_SortsByTransportationTypeId_Descending()
    {
        SetCurrentUserId(1);
        await _dbContext.SaveChangesAsync();
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, TransportationTypeId = 1 }, // Авиа
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, TransportationTypeId = 2 }  // Авто
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "transportationtypeid", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].TransportationTypeId, Is.EqualTo(2));  // Авто
        Assert.That(items[1].TransportationTypeId, Is.EqualTo(1));  // Авиа
    }

    // Sorting by CustomsProcedure.Name descending
    [Test]
    public async Task GetRegisters_SortsByCustomsProcedureId_Descending()
    {
        SetCurrentUserId(1);
        await _dbContext.SaveChangesAsync();
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, CustomsProcedureId = 1 }, // Экспорт
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, CustomsProcedureId = 2 }  // Реимпорт
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "customsprocedureid", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();

        Assert.That(items[0].CustomsProcedureId, Is.EqualTo(1));  // Экспорт
        Assert.That(items[1].CustomsProcedureId, Is.EqualTo(2));  // Реимпорт
    }

    // Sorting by InvoiceNumber descending
    [Test]
    public async Task GetRegisters_SortsByInvoiceNumber_Descending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, InvoiceNumber = "INV-001" },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, InvoiceNumber = "INV-002" }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "invoicenumber", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].InvoiceNumber, Is.EqualTo("INV-002"));
        Assert.That(items[1].InvoiceNumber, Is.EqualTo("INV-001"));
    }

    // Sorting by InvoiceDate descending
    [Test]
    public async Task GetRegisters_SortsByInvoiceDate_Descending()
    {
        SetCurrentUserId(1);
        var earlierDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var laterDate = DateOnly.FromDateTime(DateTime.Today);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, InvoiceDate = earlierDate },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, InvoiceDate = laterDate }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "invoicedate", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].Id, Is.EqualTo(2)); // Later invoice date comes first in desc order
        Assert.That(items[1].Id, Is.EqualTo(1)); // Earlier invoice date comes second
    }

    // Sorting by RecepientId ascending
    [Test]
    public async Task GetRegisters_SortsByRecepientId_Ascending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, CustomsProcedureId = 1 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, CustomsProcedureId = 2 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "recipientid", sortOrder: "asc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].Id, Is.EqualTo(2));
        Assert.That(items[1].Id, Is.EqualTo(1));
    }

    // Sorting by SenderId descending
    [Test]
    public async Task GetRegisters_SortsBySenderId_Descending()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, CustomsProcedureId = 1 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, CustomsProcedureId = 2 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "senderid", sortOrder: "desc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        var items = pr!.Items.ToArray();
        Assert.That(items[0].Id, Is.EqualTo(2));
        Assert.That(items[1].Id, Is.EqualTo(1));
    }

    // Test for invalid sort field
    [Test]
    public async Task GetRegisters_ReturnsBadRequest_WhenSortByIsInvalid()
    {
        SetCurrentUserId(1);
        var result = await _controller.GetRegisters(sortBy: "invalidfield");
        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        var obj = result.Result as ObjectResult;
        Assert.That(obj!.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    // Test that all allowed sort fields are accepted
    [Test]
    public async Task GetRegisters_AcceptsAllValidSortByValues()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.Add(new Register { Id = 1, FileName = "test.xlsx", CompanyId = 2, TheOtherCompanyId = 3 });
        await _dbContext.SaveChangesAsync();

        string[] allowedSortBy = [
            "id",
            "filename",
            "date",
            "orderstotal",
            "recepientid",
            "senderid",
            "destcountrycode",
            "origcountrycode",
            "transportationtypeid",
            "customsprocedureid",
            "invoicenumber",
            "invoicedate",
            "dealnumber"
        ];

        foreach (var sortBy in allowedSortBy)
        {
            var result = await _controller.GetRegisters(sortBy: sortBy);
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>(),
                $"sortBy '{sortBy}' should be valid");
        }
    }

    // Test sorting with null values
    [Test]
    public async Task GetRegisters_HandlesSortingWithNullValues()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, InvoiceNumber = "INV-001" },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, InvoiceNumber = null }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(sortBy: "invoiceNumber", sortOrder: "asc");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Items.Count(), Is.EqualTo(2));
        // Should not throw exception when sorting by nullable field
    }
}
