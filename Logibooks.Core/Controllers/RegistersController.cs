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

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RegistersController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<RegistersController> logger) : LogibooksControllerBase(httpContextAccessor, db, logger)
{

    private readonly string[] allowedSortBy = ["id", "filename", "date", "orderstotal"];
    private readonly int maxPageSize = 100;
    private readonly int idWBR = 2;

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
            .Select(r => new { r.Id, r.FileName, r.DTime, r.CompanyId })
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
            CompanyId = register.CompanyId, 
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
                CompanyId = r.CompanyId,
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

        int companyId = idWBR;

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
                var result = await ProcessExcel(companyId, excelContent, file.FileName);
                return result;
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

                var result = await ProcessExcel(companyId, excelContent, excelFileName);
                _logger.LogDebug("UploadRegister processed archive with Excel");
                return result;
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

        // bool hasOrders = await _db.Orders.AnyAsync(r => r.RegisterId == id);
        // if (hasOrders)
        // {
        //    return _409Register();
        // }

        _db.Registers.Remove(register);
        await _db.SaveChangesAsync();
        _logger.LogDebug("DeleteRegister returning '204 No content'");
        return NoContent();
    }

    [HttpPost("{id}/setorderstatuses")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> SetOrderStatuses(int id, int statusId)
    {
        _logger.LogDebug("SetOrderStatuses for registerId={id} statusId={statusId}", id, statusId);

        if (!await _db.CheckLogist(_curUserId))
        {
            _logger.LogDebug("SetOrderStatuses returning '403 Forbidden'");
            return _403();
        }

        if (!await _db.Registers.AnyAsync(r => r.Id == id))
        {
            _logger.LogDebug("SetOrderStatuses returning '404 Not Found' - register");
            return _404Register(id);
        }

        if (!await _db.Statuses.AnyAsync(s => s.Id == statusId))
        {
            _logger.LogDebug("SetOrderStatuses returning '404 Not Found' - status");
            return _404Status(statusId);
        }

        await _db.Orders
            .Where(o => o.RegisterId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.StatusId, statusId));

        _logger.LogDebug("SetOrderStatuses updated register {id}", id);
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

    private async Task<IActionResult> ProcessExcel(
        int companyId,
        byte[] content,
        string fileName, 
        string mappingFile = "register_mapping.yaml")
    {
        _logger.LogDebug("ProcessExcel for {file} ({size} bytes)", fileName, content.Length);

        var mappingPath = Path.Combine(AppContext.BaseDirectory, "mapping", mappingFile);
        if (!System.IO.File.Exists(mappingPath))
        {
            _logger.LogError("Mapping file not found at {path}, ProcessExcel returning '500 Internal Server Error'", mappingPath);
            return _500Mapping(mappingPath);
        }

        var mapping = RegisterMapping.Load(mappingPath);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var ms = new MemoryStream(content);
        using var reader = ExcelReaderFactory.CreateReader(ms);
        var dataSet = reader.AsDataSet();
        if (dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count <= 1)
        {
            _logger.LogDebug("ProcessExcel returning '400 Bad Request' - Excel file is empty");
            return _400EmptyRegister();
        }

        var table = dataSet.Tables[0];
        var headerRow = table.Rows[0];
        var columnMap = new Dictionary<int, string>();
        for (int c = 0; c < table.Columns.Count; c++)
        {
            var header = headerRow[c]?.ToString() ?? string.Empty;
            if (mapping.HeaderMappings.TryGetValue(header, out var prop))
            {
                columnMap[c] = prop;
            }
        }

        var register = new Register { FileName = fileName, CompanyId = companyId };
        _db.Registers.Add(register);
        await _db.SaveChangesAsync();

        Reference reference = new() { Id = register.Id };


        var orders = new List<Order>();
        for (int r = 1; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var order = new Order { RegisterId = register.Id, StatusId = 1, CheckStatusId = 1 };
            foreach (var kv in columnMap)
            {
                var val = row[kv.Key]?.ToString();
                var propInfo = typeof(Order).GetProperty(kv.Value);
                if (propInfo != null)
                {
                    if (propInfo != null && val != null)
                    {
                        try
                        {
                            // Convert string value to appropriate property type
                            object? convertedValue = ConvertValueToPropertyType(val, propInfo.PropertyType, propInfo.Name);
                            propInfo.SetValue(order, convertedValue);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to set value '{Value}' for property {Property}",
                                val, propInfo.Name);
                            // Continue with next property rather than failing the whole import
                        }
                    }
                }
            }
            orders.Add(order);
        }

        _db.Orders.AddRange(orders);
        await _db.SaveChangesAsync();

        _logger.LogDebug("ProcessExcel imported {count} orders and is returning Reference with ID: {id}", orders.Count, reference.Id);
        return CreatedAtAction(nameof(UploadRegister), new { id = reference.Id }, reference);
        
    }

    private static readonly CultureInfo RussianCulture = new("ru-RU");

    private object? ConvertValueToPropertyType(string? value, Type propertyType, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            // For nullable types or reference types, return null
            if (propertyType.IsClass || Nullable.GetUnderlyingType(propertyType) != null)
                return propertyType == typeof(string) ? string.Empty : null;

            // For value types, this will throw an exception if we don't handle specific cases below
        }

        // Get the underlying type if it's nullable
        Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        // Handle common types with specific conversion logic
        if (targetType == typeof(int) || targetType == typeof(Int32))
        {
            return int.TryParse(value, NumberStyles.Integer, RussianCulture, out int result) ? result : default;
        }
        else if (targetType == typeof(decimal))
        {
            // Handle both comma and dot as decimal separators
            string normalizedVal = value?.Replace('.', ',') ?? "0";
            return decimal.TryParse(normalizedVal, NumberStyles.AllowDecimalPoint, RussianCulture, out decimal result) ? result : default;
        }
        else if (targetType == typeof(double))
        {
            string normalizedVal = value?.Replace('.', ',') ?? "0";
            return double.TryParse(normalizedVal, NumberStyles.AllowDecimalPoint, RussianCulture, out double result) ? result : default;
        }
        else if (targetType == typeof(bool))
        {
            // Handle various representations of boolean values
            if (string.IsNullOrWhiteSpace(value))
                return default(bool);

            string normalizedVal = value.ToLower(RussianCulture).Trim();
            string[] trueValues = ["1", "yes", "true", "да"];

            if (trueValues.Contains(normalizedVal, StringComparer.InvariantCultureIgnoreCase))
                return true;
            else 
                return false;
        }
        else if (targetType == typeof(DateTime))
        {
            return DateTime.TryParse(value, RussianCulture, DateTimeStyles.None, out DateTime result) ? result : default;
        }
        else if (targetType == typeof(DateOnly))
        {
            // Handle DateOnly type - try parsing date portion only
            if (DateOnly.TryParse(value, RussianCulture, DateTimeStyles.None, out DateOnly result))
                return result;

            return default(DateOnly);
        }
        else if (targetType == typeof(string))
        {
            // For string properties, return the value directly or an empty string for null/empty input
            return value ?? string.Empty;
        }

        // For other types, try using the default conversion
        try
        {
            return Convert.ChangeType(value, targetType, RussianCulture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not convert '{Value}' to type {Type} for property {Property}",
                value, targetType.Name, propertyName);
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }
}
