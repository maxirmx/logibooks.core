// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.RestModels;
using Logibooks.Core.Models;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class StopWordsController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    ILogger<StopWordsController> logger,
    IMorphologySearchService morphologySearchService) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;
    private readonly IMorphologySearchService _morphologySearchService = morphologySearchService;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<StopWordDto>))]
    public async Task<ActionResult<IEnumerable<StopWordDto>>> GetStopWords()
    {
        var words = await _db.StopWords.AsNoTracking().OrderBy(w => w.Id).ToListAsync();
        return words.Select(w => new StopWordDto(w)).ToList();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StopWordDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<StopWordDto>> GetStopWord(int id)
    {
        var word = await _db.StopWords.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
        return word == null ? _404Object(id) : new StopWordDto(word);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(StopWordDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status418ImATeapot, Type = typeof(MorphologySupportLevelDto))]
    public async Task<ActionResult<StopWordDto>> CreateStopWord(StopWordDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();

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

        if (await _db.StopWords.AnyAsync(sw => sw.Word.ToLower() == dto.Word.ToLower()))
        {
            return _409StopWord(dto.Word);
        }
        var sw = dto.ToModel();

        _db.StopWords.Add(sw);
        try
        {
            await _db.SaveChangesAsync();
            dto.Id = sw.Id;
            return CreatedAtAction(nameof(GetStopWord), new { id = sw.Id }, dto);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_stop_words_word") == true)
        {
            _logger.LogDebug("CreateStopWord returning '409 Conflict' due to database constraint");
            return _409StopWord(dto.Word);
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status418ImATeapot, Type = typeof(MorphologySupportLevelDto))]
    public async Task<IActionResult> UpdateStopWord(int id, StopWordDto dto)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();
        var sw = await _db.StopWords.FindAsync(id);
        if (sw == null) return _404Object(id);

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

        if (!sw.Word.Equals(dto.Word, StringComparison.OrdinalIgnoreCase) &&
            await _db.StopWords.AnyAsync(w => w.Word.ToLower() == dto.Word.ToLower()))
        {
            return _409StopWord(dto.Word);
        }
        sw.Word = dto.Word;
        sw.MatchTypeId = dto.MatchTypeId;
        try
        {
            _db.Entry(sw).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_stop_words_word") == true)
        {
            _logger.LogDebug("UpdateStopWord returning '409 Conflict' due to database constraint");
            return _409StopWord(dto.Word);
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteStopWord(int id)
    {
        if (!await _userService.CheckAdmin(_curUserId)) return _403();
        var sw = await _db.StopWords.FindAsync(id);
        if (sw == null) return _404Object(id);

        _db.StopWords.Remove(sw);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
