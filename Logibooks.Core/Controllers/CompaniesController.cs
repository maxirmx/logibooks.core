using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
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
    public async Task<ActionResult<CompanyDto>> PostCompany(CompanyDto dto)
    {
        var company = dto.ToModel();
        _db.Companies.Add(company);
        await _db.SaveChangesAsync();
        dto.Id = company.Id;
        return CreatedAtAction(nameof(GetCompany), new { id = company.Id }, dto);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> PutCompany(int id, CompanyDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();

        var company = await _db.Companies.FindAsync(id);
        if (company == null) return _404Object(id);

        company.Inn = dto.Inn;
        company.Kpp = dto.Kpp;
        company.Name = dto.Name;
        company.ShortName = dto.ShortName;
        company.CountryIsoNumeric = dto.CountryIsoNumeric;
        company.PostalCode = dto.PostalCode;
        company.City = dto.City;
        company.Street = dto.Street;

        _db.Entry(company).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
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

        bool hasRegisters = await _db.Registers.AnyAsync(r => r.CompanyId == id);
        if (hasRegisters)
        {
            return _409Company();
        }

        _db.Companies.Remove(company);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
