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
public class StopWordsController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<StopWordsController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<StopWordDto>))]
    public async Task<ActionResult<IEnumerable<StopWordDto>>> GetStopWords()
    {
        var words = await _db.StopWord.AsNoTracking().OrderBy(w => w.Id).ToListAsync();
        return words.Select(w => new StopWordDto(w)).ToList();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StopWordDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<StopWordDto>> GetStopWord(int id)
    {
        var word = await _db.StopWord.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
        return word == null ? _404Object(id) : new StopWordDto(word);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(StopWordDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<StopWordDto>> PostStopWord(StopWordDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        if (await _db.StopWord.AnyAsync(sw => sw.Word.ToLower() == dto.Word.ToLower()))
        {
            return _409StopWord(dto.Word);
        }
        var sw = dto.ToModel();
        _db.StopWord.Add(sw);
        try
        {
            await _db.SaveChangesAsync();
            dto.Id = sw.Id;
            return CreatedAtAction(nameof(GetStopWord), new { id = sw.Id }, dto);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_stop_words_word") == true)
        {
            _logger.LogDebug("PostStopWord returning '409 Conflict' due to database constraint");
            return _409StopWord(dto.Word);
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> PutStopWord(int id, StopWordDto dto)
    {
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        if (id != dto.Id) return BadRequest();
        var sw = await _db.StopWord.FindAsync(id);
        if (sw == null) return _404Object(id);
        if (!sw.Word.Equals(dto.Word, StringComparison.OrdinalIgnoreCase) &&
            await _db.StopWord.AnyAsync(w => w.Word.ToLower() == dto.Word.ToLower()))
        {
            return _409StopWord(dto.Word);
        }
        sw.Word = dto.Word;
        sw.ExactMatch = dto.ExactMatch;
        try
        {
            _db.Entry(sw).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_stop_words_word") == true)
        {
            _logger.LogDebug("PutStopWord returning '409 Conflict' due to database constraint");
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
        if (!await _db.CheckAdmin(_curUserId)) return _403();
        var sw = await _db.StopWord.FindAsync(id);
        if (sw == null) return _404Object(id);

        bool hasOrders = await _db.Set<BaseOrderStopWord>().AnyAsync(r => r.StopWordId == id);
        if (hasOrders)
        {
            return _409StopWordUsed();
        }

        _db.StopWord.Remove(sw);
        try
        {
            await _db.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateException)
        {
            return _409StopWordUsed();
        }
    }
}
