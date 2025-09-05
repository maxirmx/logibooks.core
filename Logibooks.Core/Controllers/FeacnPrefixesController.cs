// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
public class FeacnPrefixesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    ILogger<FeacnPrefixesController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnPrefixDto>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<FeacnPrefixDto>>> GetPrefixes()
    {
        if (!await _userService.CheckLogist(_curUserId)) return _403();
        var prefixes = await _db.FeacnPrefixes
            .AsNoTracking()
            .Include(p => p.FeacnPrefixExceptions)
            .Where(p => p.FeacnOrderId == null)
            .OrderBy(p => p.Id)
            .Select(p => new FeacnPrefixDto(p))
            .ToListAsync();
        return prefixes;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FeacnPrefixDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<FeacnPrefixDto>> GetPrefix(int id)
    {
        if (!await _userService.CheckLogist(_curUserId)) return _403();
        var prefix = await _db.FeacnPrefixes
            .AsNoTracking()
            .Include(p => p.FeacnPrefixExceptions)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (prefix == null) return _404FeacnPrefix(id);
        if (prefix.FeacnOrderId != null) return _403FeacnPrefix(id);
        return new FeacnPrefixDto(prefix);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> CreatePrefix(FeacnPrefixCreateDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        
        // Check for duplicate code among user-created prefixes (FeacnOrderId == null)
        if (await IsCodeDuplicateAsync(dto.Code, null))
        {
            return _409DuplicateFeacnPrefixCode(dto.Code);
        }
        
        var prefix = dto.ToModel();
        
        // Silently deduplicate exception codes (case-insensitive) and filter out empty/whitespace codes
        prefix.FeacnPrefixExceptions = DeduplicateExceptionCodes(dto.Exceptions)
            .Select(e => new FeacnPrefixException { Code = e })
            .ToList();
            
        _db.FeacnPrefixes.Add(prefix);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPrefix), new { id = prefix.Id }, new Reference { Id = prefix.Id });
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdatePrefix(int id, FeacnPrefixCreateDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();

        var prefix = await _db.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (prefix == null) return _404FeacnPrefix(id);
        if (prefix.FeacnOrderId != null) return _403FeacnPrefix(id);

        // Check for duplicate code among user-created prefixes (excluding current prefix)
        if (await IsCodeDuplicateAsync(dto.Code, id))
        {
            return _409DuplicateFeacnPrefixCode(dto.Code);
        }

        prefix.Code = dto.Code;
        prefix.IntervalCode = dto.IntervalCode;
        prefix.Description = dto.Description;
        prefix.Comment = dto.Comment;

        _db.FeacnPrefixExceptions.RemoveRange(prefix.FeacnPrefixExceptions);
        
        // Silently deduplicate exception codes (case-insensitive) and filter out empty/whitespace codes
        prefix.FeacnPrefixExceptions = DeduplicateExceptionCodes(dto.Exceptions)
            .Select(e => new FeacnPrefixException { Code = e, FeacnPrefixId = id })
            .ToList();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeletePrefix(int id)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        var prefix = await _db.FeacnPrefixes
            .Include(p => p.FeacnPrefixExceptions)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (prefix == null) return _404FeacnPrefix(id);
        if (prefix.FeacnOrderId != null) return _403FeacnPrefix(id);

        _db.FeacnPrefixExceptions.RemoveRange(prefix.FeacnPrefixExceptions);
        _db.FeacnPrefixes.Remove(prefix);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Checks if a given code already exists among user-created prefixes (FeacnOrderId == null)
    /// </summary>
    /// <param name="code">The code to check for duplicates</param>
    /// <param name="excludeId">ID to exclude from the check (for updates)</param>
    /// <returns>True if duplicate exists, false otherwise</returns>
    private async Task<bool> IsCodeDuplicateAsync(string code, int? excludeId)
    {
        var query = _db.FeacnPrefixes
            .Where(p => p.FeacnOrderId == null && p.Code == code);
        
        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }
        
        return await query.AnyAsync();
    }

    /// <summary>
    /// Deduplicates exception codes (case-insensitive) and filters out empty/whitespace codes
    /// </summary>
    /// <param name="exceptionCodes">List of exception codes to deduplicate</param>
    /// <returns>List of unique, non-empty exception codes</returns>
    private static List<string> DeduplicateExceptionCodes(List<string> exceptionCodes)
    {
        if (exceptionCodes == null || exceptionCodes.Count == 0)
            return [];
            
        var uniqueCodes = new List<string>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var code in exceptionCodes)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var trimmedCode = code.Trim();
                if (seenCodes.Add(trimmedCode))
                {
                    uniqueCodes.Add(trimmedCode);
                }
            }
        }
        
        return uniqueCodes;
    }

}

