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

using Microsoft.AspNetCore.Mvc;

using System.Linq;
using System.Threading.Tasks;

using NUnit.Framework;

using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Tests.Controllers.Registers;

[TestFixture]
public class RegistersControllerSearchTests : RegistersControllerTestsBase
{
    [Test]
    public async Task GetRegisters_SearchFiltersByFileName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "report1.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "other.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "report");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().FileName, Is.EqualTo("report1.xlsx"));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByInvoiceNumber()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, InvoiceNumber = "INV-123" },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, InvoiceNumber = "INV-456" }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "123");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().InvoiceNumber, Is.EqualTo("INV-123"));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByDealNumber()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, DealNumber = "DEAL-2024-001" },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, DealNumber = "DEAL-2024-002" }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "001");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().DealNumber, Is.EqualTo("DEAL-2024-001"));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByCompanyShortName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 1, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "РВБ");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().CompanyId, Is.EqualTo(2));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByTheOtherCompanyShortName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 1, TheOtherCompanyId = 1 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "Узбекпочта");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().TheOtherCompanyId, Is.EqualTo(3));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByCountryName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3, TheOtherCountryCode = 643 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, TheOtherCountryCode = 860 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "Узбекистан");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().TheOtherCountryCode, Is.EqualTo(860));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByTransportationTypeName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 1, TheOtherCompanyId = 3, TransportationTypeId = 2 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, TransportationTypeId = 1 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "Авиа");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().TransportationTypeId, Is.EqualTo(1));
    }

    [Test]
    public async Task GetRegisters_SearchFiltersByCustomsProcedureName()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 1, TheOtherCompanyId = 3, CustomsProcedureId = 2 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 2, TheOtherCompanyId = 3, CustomsProcedureId = 1 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "Экспорт");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().CustomsProcedureId, Is.EqualTo(1));
    }

    [Test]
    public async Task GetRegisters_SearchIsCaseInsensitive()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "report.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "other.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "REPORT");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().FileName, Is.EqualTo("report.xlsx"));
    }

    [Test]
    public async Task GetRegisters_SearchWithPartialMatches()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "quarterly_report.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "monthly_data.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "quarter");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(1));
        Assert.That(pr.Items.First().FileName, Is.EqualTo("quarterly_report.xlsx"));
    }

    [Test]
    public async Task GetRegisters_SearchIgnoresRussiaKeyword()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "r1.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "r2.xlsx", CompanyId = 1, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "Россия");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(2));
    }

    [Test]
    public async Task GetRegisters_SearchReturnsZeroWhenNoMatch()
    {
        SetCurrentUserId(1);
        _dbContext.Registers.AddRange(
            new Register { Id = 1, FileName = "f1.xlsx", CompanyId = 2, TheOtherCompanyId = 3 },
            new Register { Id = 2, FileName = "f2.xlsx", CompanyId = 2, TheOtherCompanyId = 3 }
        );
        await _dbContext.SaveChangesAsync();
        var result = await _controller.GetRegisters(search: "nomatch");
        var ok = result.Result as OkObjectResult;
        var pr = ok!.Value as PagedResult<RegisterViewItem>;
        Assert.That(pr!.Pagination.TotalCount, Is.EqualTo(0));
    }
}
