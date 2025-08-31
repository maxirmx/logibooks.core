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
using SharpCompress.Archives;
using System.IO;
using System.Linq;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
public class FeacnCodesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<FeacnCodesController> logger,
    IFeacnListProcessingService processingService) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IFeacnListProcessingService _processingService = processingService;
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FeacnCodeDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<FeacnCodeDto>> Get(int id)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var code = await _db.FeacnCodes.AsNoTracking()
            .Where(c => c.Id == id && (c.FromDate == null || c.FromDate <= today))
            .FirstOrDefaultAsync();
        return code == null ? _404Object(id) : new FeacnCodeDto(code);
    }

    [HttpGet("code/{code}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FeacnCodeDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<FeacnCodeDto>> GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            code.Length != FeacnCode.FeacnCodeLength ||
            !code.All(char.IsDigit))
        {
            return _400MustBe10Digits(code);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fc = await _db.FeacnCodes.AsNoTracking()
            .Where(c => c.Code == code && (c.FromDate == null || c.FromDate <= today))
            .FirstOrDefaultAsync();
        return fc == null ? _404FeacnCode(code) : new FeacnCodeDto(fc);
    }

    [HttpGet("lookup/{key}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnCodeDto>))]
    public async Task<ActionResult<IEnumerable<FeacnCodeDto>>> Lookup(string key)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        IQueryable<FeacnCode> query = _db.FeacnCodes.AsNoTracking()
            .Where(c => c.FromDate == null || c.FromDate <= today);

        // Handle empty or whitespace-only keys
        if (string.IsNullOrWhiteSpace(key))
        {
            return new List<FeacnCodeDto>();
        }

        // If key contains only digits, search by code prefix; otherwise search by normalized name
        if (key.All(char.IsDigit))
        {
            query = query.Where(c => c.Code.StartsWith(key));
        }
        else
        {
            var upperKey = key.ToUpper();
            query = query.Where(c => c.NormalizedName.Contains(upperKey));
        }

        var codes = await query
            .OrderBy(c => c.Id)
            .Select(c => new FeacnCodeDto(c))
            .ToListAsync();
        
        return codes;
    }

    [HttpGet("children")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<FeacnCodeDto>))]
    public async Task<ActionResult<IEnumerable<FeacnCodeDto>>> Children(int? id)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        IQueryable<FeacnCode> query = _db.FeacnCodes.AsNoTracking()
            .Where(c => c.FromDate == null || c.FromDate <= today);
        
        query = id.HasValue ? query.Where(c => c.ParentId == id.Value) : query.Where(c => c.ParentId == null);
        
        var codes = await query
            .OrderBy(c => c.Id)
            .Select(c => new FeacnCodeDto(c))
            .ToListAsync();
        return codes;
    }

    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return _400();
        }

        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        byte[] excelContent = [];
        string excelFileName = string.Empty;

        if (fileExtension == ".xlsx" || fileExtension == ".xls")
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            excelContent = ms.ToArray();
            excelFileName = file.FileName;
        }
        else if (fileExtension == ".zip" || fileExtension == ".rar")
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;

            using var archive = ArchiveFactory.Open(ms);
            var excelEntry = archive.Entries.FirstOrDefault(entry =>
                !entry.IsDirectory &&
                entry.Key != null &&
                (Path.GetExtension(entry.Key).Equals(".xlsx", StringComparison.InvariantCultureIgnoreCase) ||
                 Path.GetExtension(entry.Key).Equals(".xls", StringComparison.InvariantCultureIgnoreCase)));

            if (excelEntry == null || excelEntry.Key == null)
            {
                return _400NoRegister();
            }

            excelFileName = excelEntry.Key;
            using var entryStream = new MemoryStream();
            excelEntry.WriteTo(entryStream);
            excelContent = entryStream.ToArray();
        }
        else
        {
            return _400UnsupportedFileType(fileExtension);
        }

        await _processingService.UploadFeacnCodesAsync(excelContent, excelFileName, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("bulk-lookup")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BulkFeacnCodeLookupDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    public async Task<ActionResult<BulkFeacnCodeLookupDto>> BulkLookup([FromBody] BulkFeacnCodeRequestDto request)
    {
        if (request?.Codes == null)
        {
            return new BulkFeacnCodeLookupDto();
        }

        var result = new Dictionary<string, FeacnCodeDto?>();
        
        // Validate codes and initialize result dictionary
        var validCodes = new List<string>();
        foreach (var code in request.Codes)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue; // Skip null/empty codes silently
            }
            
            if (code.Length != FeacnCode.FeacnCodeLength || !code.All(char.IsDigit))
            {
                return _400MustBe10Digits(code);
            }
            
            result[code] = null; // Initialize with null
            validCodes.Add(code);
        }

        if (validCodes.Count == 0)
        {
            return new BulkFeacnCodeLookupDto(result);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        // Bulk query to get all matching codes
        var foundCodes = await _db.FeacnCodes.AsNoTracking()
            .Where(c => validCodes.Contains(c.Code) && (c.FromDate == null || c.FromDate <= today))
            .ToListAsync();

        // Populate result dictionary with found codes
        foreach (var feacnCode in foundCodes)
        {
            result[feacnCode.Code] = new FeacnCodeDto(feacnCode);
        }

        return new BulkFeacnCodeLookupDto(result);
    }
}

