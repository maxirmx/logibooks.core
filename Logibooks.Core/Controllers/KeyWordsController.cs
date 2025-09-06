// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.RestModels;
using Logibooks.Core.Models;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
public class KeyWordsController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    IKeywordsProcessingService processingService,
    IMorphologySearchService morphologySearchService,
    ILogger<KeyWordsController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;
    private readonly IKeywordsProcessingService _processingService = processingService;
    private readonly IMorphologySearchService _morphologySearchService = morphologySearchService;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<KeyWordDto>))]
    public async Task<ActionResult<IEnumerable<KeyWordDto>>> GetKeyWords()
    {
        var words = await _db.KeyWords
            .Include(w => w.KeyWordFeacnCodes)
            .AsNoTracking()
            .OrderBy(w => w.Id)
            .ToListAsync();
        return words.Select(w => new KeyWordDto(w)).ToList();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(KeyWordDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<KeyWordDto>> GetKeyWord(int id)
    {
        var word = await _db.KeyWords
            .Include(w => w.KeyWordFeacnCodes)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);
        return word == null ? _404Object(id) : new KeyWordDto(word);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(KeyWordDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status418ImATeapot, Type = typeof(MorphologySupportLevelDto))]
    public async Task<ActionResult<KeyWordDto>> CreateKeyWord(KeyWordDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();

        // Validate FeacnCodes: empty list is OK, but if provided, each must be exactly 10 digits
        if (dto.FeacnCodes != null && dto.FeacnCodes.Count > 0)
        {
            foreach (var feacnCode in dto.FeacnCodes)
            {
                if (string.IsNullOrWhiteSpace(feacnCode) || feacnCode.Length != 10 || !feacnCode.All(char.IsDigit))
                {
                    return _400MustBe10Digits(feacnCode);
                }
            }
        }

        if (dto.MatchTypeId >= (int)WordMatchTypeCode.MorphologyMatchTypes)
        {
            var checkResult = _morphologySearchService.CheckWord(dto.Word);
            if (
                checkResult == MorphologySupportLevel.NoSupport ||
                (dto.MatchTypeId >= (int)WordMatchTypeCode.StrongMorphology && checkResult == MorphologySupportLevel.FormsSupport)
            )
            {
                return StatusCode(StatusCodes.Status418ImATeapot, new MorphologySupportLevelDto {
                    Word = dto.Word,
                    Level = (int)checkResult
                });
            }
        }

        // Check for conflicts: prevent duplicate words (regardless of FeacnCodes)
        if (await _db.KeyWords.AnyAsync(sw => sw.Word.ToLower() == dto.Word.ToLower()))
        {
            return _409KeyWord(dto.Word);
        }

        var kw = dto.ToModel();
        _db.KeyWords.Add(kw);
        try
        {
            await _db.SaveChangesAsync();
            dto.Id = kw.Id;
            return CreatedAtAction(nameof(GetKeyWord), new { id = kw.Id }, dto);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_key_words_word") == true)
        {
            _logger.LogDebug("CreateKeyWord returning '409 Conflict' due to database constraint");
            return _409KeyWord(dto.Word);
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status418ImATeapot, Type = typeof(MorphologySupportLevelDto))]
    public async Task<IActionResult> UpdateKeyWord(int id, KeyWordDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();

        // Validate FeacnCodes: empty list is OK, but if provided, each must be exactly 10 digits
        if (dto.FeacnCodes != null && dto.FeacnCodes.Count > 0)
        {
            foreach (var feacnCode in dto.FeacnCodes)
            {
                if (string.IsNullOrWhiteSpace(feacnCode) || feacnCode.Length != 10 || !feacnCode.All(char.IsDigit))
                {
                    return _400MustBe10Digits(feacnCode);
                }
            }
        }

        var kw = await _db.KeyWords
            .Include(w => w.KeyWordFeacnCodes)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (kw == null) return _404Object(id);

        if (dto.MatchTypeId >= (int)WordMatchTypeCode.MorphologyMatchTypes)
        {
            var checkResult = _morphologySearchService.CheckWord(dto.Word);
            if (
                checkResult == MorphologySupportLevel.NoSupport ||
                (dto.MatchTypeId >= (int)WordMatchTypeCode.StrongMorphology && checkResult == MorphologySupportLevel.FormsSupport)
            )
            {
                return StatusCode(StatusCodes.Status418ImATeapot, new MorphologySupportLevelDto {
                    Word = dto.Word,
                    Level = (int)checkResult
                });
            }
        }

        // Check for conflicts: prevent duplicate words (regardless of FeacnCodes)
        if (!kw.Word.Equals(dto.Word, StringComparison.OrdinalIgnoreCase) &&
            await _db.KeyWords.AnyAsync(w => w.Word.ToLower() == dto.Word.ToLower()))
        {
            return _409KeyWord(dto.Word);
        }

        try
        {
            // Update the KeyWord properties
            kw.Word = dto.Word;
            kw.MatchTypeId = dto.MatchTypeId;
       
            // Get current FeacnCodes
            var currentCodes = kw.KeyWordFeacnCodes.ToList();
            var newCodes = dto.FeacnCodes ?? [];
            
            // Find codes to remove
            var codesToRemove = currentCodes.Where(c => !newCodes.Contains(c.FeacnCode)).ToList();
            foreach (var codeToRemove in codesToRemove)
            {
                kw.KeyWordFeacnCodes.Remove(codeToRemove);
            }
            
            // Find codes to add
            var existingCodes = currentCodes.Select(c => c.FeacnCode).ToHashSet();
            var codesToAdd = newCodes.Where(nc => !existingCodes.Contains(nc)).ToList();
            foreach (var codeToAdd in codesToAdd)
            {
                kw.KeyWordFeacnCodes.Add(new KeyWordFeacnCode
                {
                    KeyWordId = kw.Id,
                    FeacnCode = codeToAdd,
                    KeyWord = kw
                });
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_key_words_word") == true)
        {
            _logger.LogDebug("UpdateKeyWord returning '409 Conflict' due to database constraint");
            return _409KeyWord(dto.Word);
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteKeyWord(int id)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        var kw = await _db.KeyWords.FindAsync(id);
        if (kw == null) return _404Object(id);

        _db.KeyWords.Remove(kw);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        try
        {
            await _processingService.UploadKeywordsFromExcelAsync(ms.ToArray(), file.FileName);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return _400KeyWordFile(ex.Message);
        }
    }
}
