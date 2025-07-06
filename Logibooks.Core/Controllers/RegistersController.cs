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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
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
using System.Globalization;

using SharpCompress.Archives;
using ExcelDataReader;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Settings;
using Logibooks.Core.Services;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RegistersController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<RegistersController> logger,
    RegisterProcessingService registerService) : LogibooksControllerBase(httpContextAccessor, db, logger)
{

    private readonly RegisterProcessingService _registerService = registerService;

    private readonly string[] allowedSortBy = ["id", "filename", "date", "orderstotal"];
    private readonly int maxPageSize = 100;

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RegisterViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<RegisterViewItem>> GetRegister(int id)
    {
        _logger.LogDebug("GetRegister for id={id}", id);

        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetRegister returning '403 Forbidden'");
            return _403();
        }

        var register = await _db.Registers
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.Id, r.FileName, r.DTime })
            .FirstOrDefaultAsync();

        if (register == null)
        {
            _logger.LogDebug("GetRegister returning '404 Not Found'");
            return _404Register(id);
        }

        var ordersStats = await FetchOrdersStatsAsync([id]);
        var ordersByStatus = ordersStats.GetValueOrDefault(id, new Dictionary<int, int>());

        var view = new RegisterViewItem
        {
            Id = register.Id,
            FileName = register.FileName,
            Date = register.DTime,
            OrdersTotal = ordersByStatus.Values.Sum(),
            OrdersByStatus = ordersByStatus
        };

        _logger.LogDebug("GetRegister returning register with {count} orders", view.OrdersTotal);
        return view;
    }
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<RegisterViewItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<PagedResult<RegisterViewItem>>> GetRegisters(
        int page = 1,
        int pageSize = 10,
        string? sortBy = null,
        string sortOrder = "asc",
        string? search = null)
    {
        _logger.LogDebug("GetRegisters for page={page} pageSize={size} sortBy={sortBy} sortOrder={sortOrder} search={search}",
            page, pageSize, sortBy, sortOrder, search);

        if (page <= 0 ||
            (pageSize != -1 && (pageSize <= 0 || pageSize > maxPageSize)))
        {
            _logger.LogDebug("GetRegisters returning '400 Bad Request' - invalid pagination");
            return _400();
        }

        sortBy ??= "id";
        sortOrder = string.IsNullOrEmpty(sortOrder) ? "asc" : sortOrder.ToLower();

        if (!allowedSortBy.Contains(sortBy.ToLower()))
        {
            _logger.LogDebug("GetRegisters returning '400 Bad Request' - invalid sortBy");
            return _400();
        }

        if (sortOrder != "asc" && sortOrder != "desc")
        {
            _logger.LogDebug("GetRegisters returning '400 Bad Request' - invalid sortOrder");
            return _400();
        }

        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetRegisters returning '403 Forbidden'");
            return _403();
        }

        // Build base query for registers
        IQueryable<Register> baseQuery = _db.Registers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            baseQuery = baseQuery.Where(r => EF.Functions.Like(r.FileName, $"%{search}%"));
        }
        // Project to view items with orders total included for sorting
        IQueryable<RegisterViewItem> query = baseQuery
            .Select(r => new RegisterViewItem
            {
                Id = r.Id,
                FileName = r.FileName,
                Date = r.DTime,
                OrdersTotal = r.Orders.Count()
            });

        query = (sortBy.ToLower(), sortOrder) switch
        {
            ("filename", "asc") => query.OrderBy(r => r.FileName),
            ("filename", "desc") => query.OrderByDescending(r => r.FileName),
            ("date", "asc") => query.OrderBy(r => r.Date),
            ("date", "desc") => query.OrderByDescending(r => r.Date),
            ("orderstotal", "asc") => query.OrderBy(r => r.OrdersTotal),
            ("orderstotal", "desc") => query.OrderByDescending(r => r.OrdersTotal),
            ("id", "desc") => query.OrderByDescending(r => r.Id),
            _ => query.OrderBy(r => r.Id)
        };

        var totalCount = await baseQuery.CountAsync();

        int actualPage = pageSize == -1 ? 1 : page;
        int actualPageSize = pageSize == -1 ? (totalCount == 0 ? 1 : totalCount) : pageSize;

        var totalPages = (int)Math.Ceiling(totalCount / (double)actualPageSize);

        if (actualPage > totalPages && totalPages > 0)
        {
            actualPage = 1;
        }

        var items = await query
            .Skip((actualPage - 1) * actualPageSize)
            .Take(actualPageSize)
            .ToListAsync();
        var ids = items.Select(r => r.Id).ToList();
        var stats = await FetchOrdersStatsAsync(ids);

        foreach (var item in items)
        {
            var byStatus = stats.GetValueOrDefault(item.Id, new Dictionary<int, int>());
            item.OrdersByStatus = byStatus;
            item.OrdersTotal = byStatus.Values.Sum();
        }

        var result = new PagedResult<RegisterViewItem>
        {
            Items = items,
            Pagination = new PaginationInfo
            {
                CurrentPage = actualPage,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = actualPage < totalPages,
                HasPreviousPage = actualPage > 1
            },
            Sorting = new SortingInfo { SortBy = sortBy, SortOrder = sortOrder },
            Search = search
        };

        _logger.LogDebug("GetRegisters returning count: {count} items", items.Count);
        return Ok(result);
    }

    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UploadRegister(IFormFile file)
    {
        _logger.LogDebug("UploadRegister called for {name} ({size} bytes)", file?.FileName, file?.Length);

        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("UploadRegister returning '403 Forbidden'");
            return _403();
        }

        if (file == null || file.Length == 0)
        {
            _logger.LogDebug("UploadRegister returning '400 Bad Request' - empty file");
            return _400EmptyRegister();
        }

        try
        {
            // Get the file extension
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            // Handle based on file type
            if (fileExtension == ".xlsx" || fileExtension == ".xls")
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                byte[] excelContent = ms.ToArray();
                var result = await _registerService.ProcessExcelAsync(excelContent, file.FileName);
                if (result.Error == ProcessExcelError.EmptyExcel)
                    return _400EmptyRegister();
                if (result.Error == ProcessExcelError.MappingNotFound)
                    return _500Mapping(result.MappingPath!);
                return CreatedAtAction(nameof(UploadRegister), new { id = result.Reference!.Id }, result.Reference);
            }
            else if (fileExtension == ".zip" || fileExtension == ".rar")
            {
                // Archive file - need to extract Excel
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;

                byte[] excelContent = [];
                string excelFileName = String.Empty;

                // Extract content from archive
                using (var archive = ArchiveFactory.Open(ms))
                {
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

                    // Extract the Excel file
                    using var entryStream = new MemoryStream();
                    excelEntry.WriteTo(entryStream);
                    excelContent = entryStream.ToArray();
                }

                var result = await _registerService.ProcessExcelAsync(excelContent, excelFileName);
                _logger.LogDebug("UploadRegister processed archive with Excel");
                if (result.Error == ProcessExcelError.EmptyExcel)
                    return _400EmptyRegister();
                if (result.Error == ProcessExcelError.MappingNotFound)
                    return _500Mapping(result.MappingPath!);
                return CreatedAtAction(nameof(UploadRegister), new { id = result.Reference!.Id }, result.Reference);
            }
            else
            {
                _logger.LogDebug("UploadRegister returning '400 Bad Request' - unsupported file type {ext}", fileExtension);
                return _400UnsupportedFileType(fileExtension);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UploadRegister returning '500 Internal Server Error'");
            return _500UploadRegister();
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteRegister(int id)
    {
        _logger.LogDebug("DeleteRegister for id={id}", id);

        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("DeleteRegister returning '403 Forbidden'");
            return _403();
        }

        var register = await _db.Registers
            .Include(r => r.Orders)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (register == null)
        {
            _logger.LogDebug("DeleteRegister returning '404 Not Found'");
            return _404Register(id);
        }

        _db.Registers.Remove(register);
        await _db.SaveChangesAsync();
        _logger.LogDebug("DeleteRegister returning '204 No content'");
        return NoContent();
    }

    private async Task<Dictionary<int, Dictionary<int, int>>> FetchOrdersStatsAsync(IEnumerable<int> registerIds)
    {
        var grouped = await _db.Orders
            .AsNoTracking()
            .Where(o => registerIds.Contains(o.RegisterId))
            .GroupBy(o => new { o.RegisterId, o.StatusId })
            .Select(g => new { g.Key.RegisterId, g.Key.StatusId, Count = g.Count() })
            .ToListAsync();

        return grouped
            .GroupBy(g => g.RegisterId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.StatusId, x => x.Count));
    }

}
