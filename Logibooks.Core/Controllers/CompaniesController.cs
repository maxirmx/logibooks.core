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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CompaniesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<CompaniesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{

    const int _companyOzon = 1; // Ozon company ID
    const int _companyWBR = 2;  // WBR company ID

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<CompanyDto>))]
    public async Task<ActionResult<IEnumerable<CompanyDto>>> GetCompanies()
    {
        var list = await _db.Companies.Select(c => new CompanyDto(c)).ToListAsync();
        return list;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CompanyDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<CompanyDto>> GetCompany(int id)
    {
        var company = await _db.Companies.FindAsync(id);
        return company == null ? _404Object(id) : new CompanyDto(company);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CompanyDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<CompanyDto>> PostCompany(CompanyDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        if (await _db.Companies.AnyAsync(c => c.Inn == dto.Inn))
        {
            return _409CompanyInn(dto.Inn);
        }
        try
        {
            var company = dto.ToModel();
            _db.Companies.Add(company);
            await _db.SaveChangesAsync();
            dto.Id = company.Id;
            return CreatedAtAction(nameof(GetCompany), new { id = company.Id }, dto);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_companies_inn") == true)
        {
            // Handle database constraint violation (race condition case)
            _logger.LogDebug("PostCompany returning '409 Conflict' due to database constraint");
            return _409CompanyInn(dto.Inn);
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> PutCompany(int id, CompanyDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();

        var company = await _db.Companies.FindAsync(id);
        if (company == null) return _404Object(id);

        if (company.Inn != dto.Inn && await _db.Companies.AnyAsync(c => c.Inn == dto.Inn))
        {
            return _409CompanyInn(dto.Inn);
        }

        company.Inn = dto.Inn;
        company.Kpp = dto.Kpp;
        company.Name = dto.Name;
        company.ShortName = dto.ShortName;
        company.CountryIsoNumeric = dto.CountryIsoNumeric;
        company.PostalCode = dto.PostalCode;
        company.City = dto.City;
        company.Street = dto.Street;
        try
        {
            _db.Entry(company).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_companies_inn") == true)
        {
            // Handle database constraint violation (race condition case)
            _logger.LogDebug("PutCompany returning '409 Conflict' due to database constraint");
            return _409CompanyInn(dto.Inn);
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteCompany(int id)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        var company = await _db.Companies.FindAsync(id);
        if (company == null) return _404Object(id);

        bool hasRegisters = id == _companyWBR || id == _companyOzon || await _db.Registers.AnyAsync(r => r.CompanyId == id);
        if (hasRegisters)
        {
            return _409Company();
        }

        _db.Companies.Remove(company);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
