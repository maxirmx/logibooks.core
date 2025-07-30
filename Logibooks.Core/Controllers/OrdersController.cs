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

using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.Extensions;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Services;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class OrdersController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    ILogger<OrdersController> logger,
    IMapper mapper,
    IOrderValidationService validationService,
    IMorphologySearchService morphologyService,
    IRegisterProcessingService processingService,
    IOrderIndPostGenerator indPostGenerator) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private const int MaxPageSize = 1000;
    private readonly IUserInformationService _userService = userService;
    private readonly IMapper _mapper = mapper;
    private readonly IOrderValidationService _validationService = validationService;
    private readonly IMorphologySearchService _morphologyService = morphologyService;
    private readonly IRegisterProcessingService _processingService = processingService;
    private readonly IOrderIndPostGenerator _indPostGenerator = indPostGenerator;

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<OrderViewItem>> GetOrder(int id)
    {
        _logger.LogDebug("GetOrder for id={id}", id);

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetOrder returning '403 Forbidden'");
            return _403();
        }

        // First, get the order with its register to determine the company type
        var orderWithRegister = await _db.Orders.AsNoTracking()
            .Include(o => o.Register)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (orderWithRegister == null)
        {
            _logger.LogDebug("GetOrder returning '404 Not Found'");
            return _404Order(id);
        }

        // Validate that the company exists
        if (!await _db.Companies.AnyAsync(c => c.Id == orderWithRegister.Register.CompanyId))
        {
            _logger.LogDebug("GetOrder returning '404 Not Found' - company not found");
            return _404CompanyId(orderWithRegister.Register.CompanyId);
        }

        // Now query the specific order type with all required includes
        BaseOrder? order = null;
        int companyId = orderWithRegister.Register.CompanyId;

        if (companyId == IRegisterProcessingService.GetWBRId())
        {
            order = await _db.WbrOrders.AsNoTracking()
                .Include(o => o.Register)
                .Include(o => o.BaseOrderStopWords)
                .Include(o => o.BaseOrderFeacnPrefixes)
                    .ThenInclude(bofp => bofp.FeacnPrefix)
                        .ThenInclude(fp => fp.FeacnOrder)
                .FirstOrDefaultAsync(o => o.Id == id);
        }
        else if (companyId == IRegisterProcessingService.GetOzonId())
        {
            order = await _db.OzonOrders.AsNoTracking()
                .Include(o => o.Register)
                .Include(o => o.BaseOrderStopWords)
                .Include(o => o.BaseOrderFeacnPrefixes)
                    .ThenInclude(bofp => bofp.FeacnPrefix)
                        .ThenInclude(fp => fp.FeacnOrder)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        if (order == null)
        {
            _logger.LogDebug("GetOrder returning '404 Not Found' - order not found in specific table");
            return _404Order(id);
        }

        _logger.LogDebug("GetOrder returning {orderType} order for companyId={cid}",
            order.GetType().Name, companyId);
        return new OrderViewItem(order);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateOrder(int id, OrderUpdateItem update)
    {
        _logger.LogDebug("UpdateOrder for id={id}", id);

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("UpdateOrder returning '403 Forbidden'");
            return _403();
        }

        // First, get the order with its register to determine the company type
        var orderWithRegister = await _db.Orders
            .Include(o => o.Register)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (orderWithRegister == null)
        {
            _logger.LogDebug("UpdateOrder returning '404 Not Found'");
            return _404Order(id);
        }

        // Validate that the company exists
        if (!await _db.Companies.AnyAsync(c => c.Id == orderWithRegister.Register.CompanyId))
        {
            _logger.LogDebug("UpdateOrder returning '404 Not Found' - company not found");
            return _404CompanyId(orderWithRegister.Register.CompanyId);
        }

        // Now query the specific order type for update
        BaseOrder? order = null;
        int companyId = orderWithRegister.Register.CompanyId;

        if (companyId == IRegisterProcessingService.GetWBRId())
        {
            order = await _db.WbrOrders
                .Include(o => o.Register)
                .FirstOrDefaultAsync(o => o.Id == id);
        }
        else if (companyId == IRegisterProcessingService.GetOzonId())
        {
            order = await _db.OzonOrders
                .Include(o => o.Register)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        if (order == null)
        {
            _logger.LogDebug("UpdateOrder returning '404 Not Found' - order not found in specific table");
            return _404Order(id);
        }

        if (order is WbrOrder wbr)
        {
            wbr.UpdateFrom(update, _mapper);
        }
        else if (order is OzonOrder ozon)
        {
            ozon.UpdateFrom(update, _mapper);
        }

        _db.Entry(order).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        _logger.LogDebug("UpdateOrder returning '204 No content' for id={id}", id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        _logger.LogDebug("DeleteOrder for id={id}", id);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("DeleteOrder returning '403 Forbidden'");
            return _403();
        }

        // First, get the order with its register to determine the company type
        var orderWithRegister = await _db.Orders
            .Include(o => o.Register)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (orderWithRegister == null)
        {
            _logger.LogDebug("DeleteOrder returning '404 Not Found'");
            return _404Order(id);
        }

        // Validate that the company exists
        if (!await _db.Companies.AnyAsync(c => c.Id == orderWithRegister.Register.CompanyId))
        {
            _logger.LogDebug("DeleteOrder returning '404 Not Found' - company not found");
            return _404CompanyId(orderWithRegister.Register.CompanyId);
        }

        _db.Remove(orderWithRegister);
        await _db.SaveChangesAsync();

        _logger.LogDebug("DeleteOrder returning '204 No content' for id={id}", id);
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<OrderViewItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<PagedResult<OrderViewItem>>> GetOrders(
        int registerId,
        int? statusId = null,
        string? tnVed = null,
        int page = 1,
        int pageSize = 100,
        string? sortBy = null,
        string sortOrder = "asc")
    {
        _logger.LogDebug("GetOrders for register={reg} status={st} tnVed={tnVed} page={page} size={size} sortBy={sortBy} sortOrder={sortOrder}",
            registerId, statusId, tnVed, page, pageSize, sortBy, sortOrder);

        if (page <= 0 ||
            (pageSize != -1 && (pageSize <= 0 || pageSize > MaxPageSize)))
        {
            _logger.LogDebug("GetOrders returning '400 Bad Request' - invalid pagination");
            return _400();
        }

        sortBy ??= "id";
        sortOrder = string.IsNullOrEmpty(sortOrder) ? "asc" : sortOrder.ToLower();

        var allowedSortBy = new[] { "id", "status", "tnved" };
        if (!allowedSortBy.Contains(sortBy.ToLower()))
        {
            _logger.LogDebug("GetOrders returning '400 Bad Request' - invalid sortBy");
            return _400();
        }

        if (sortOrder != "asc" && sortOrder != "desc")
        {
            _logger.LogDebug("GetOrders returning '400 Bad Request' - invalid sortOrder");
            return _400();
        }

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetOrders returning '403 Forbidden'");
            return _403();
        }

        IQueryable<BaseOrder> query = _db.Orders.AsNoTracking()
            .Include(o => o.BaseOrderStopWords)
            .Include(o => o.BaseOrderFeacnPrefixes)
                .ThenInclude(bofp => bofp.FeacnPrefix)
                    .ThenInclude(fp => fp.FeacnOrder)
            .Where(o => o.RegisterId == registerId);

        if (statusId != null)
        {
            query = query.Where(o => o.StatusId == statusId);
        }

        if (!string.IsNullOrWhiteSpace(tnVed))
        {
            query = query.Where(o => o.TnVed != null && o.TnVed.Contains(tnVed));
        }

        query = (sortBy.ToLower(), sortOrder) switch
        {
            ("status", "asc") => query.OrderBy(o => o.StatusId),
            ("status", "desc") => query.OrderByDescending(o => o.StatusId),
            ("tnved", "asc") => query.OrderBy(o => o.TnVed),
            ("tnved", "desc") => query.OrderByDescending(o => o.TnVed),
            ("id", "desc") => query.OrderByDescending(o => o.Id),
            _ => query.OrderBy(o => o.Id)
        };

        var totalCount = await query.CountAsync();

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

        var viewItems = items.Select(o => new OrderViewItem(o)).ToList();

        var result = new PagedResult<OrderViewItem>
        {
            Items = viewItems,
            Pagination = new PaginationInfo
            {
                CurrentPage = actualPage,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = actualPage < totalPages,
                HasPreviousPage = actualPage > 1
            },
            Sorting = new SortingInfo { SortBy = sortBy, SortOrder = sortOrder }
        };

        _logger.LogDebug("GetOrders returning {count} items", items.Count);
        return Ok(result);
    }

    [HttpPost("{id}/validate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> ValidateOrder(int id)
    {
        _logger.LogDebug("ValidateOrder for id={id}", id);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("ValidateOrder returning '403 Forbidden'");
            return _403();
        }

        var order = await _db.Orders.FindAsync(id);
        if (order == null)
        {
            _logger.LogDebug("ValidateOrder returning '404 Not Found'");
            return _404Order(id);
        }

        var stopWords = await _db.StopWords.AsNoTracking().ToListAsync();
        var mSw = stopWords.Where(sw => sw.MatchTypeId >= (int)StopWordMatchTypeCode.MorphologyMatchTypes).ToList();
        var morphologyContext = _morphologyService.InitializeContext(mSw);
        var stopWordsContext = _validationService.InitializeStopWordsContext(stopWords.Where(sw => sw.MatchTypeId < (int)StopWordMatchTypeCode.MorphologyMatchTypes));

        await _validationService.ValidateAsync(order, morphologyContext, stopWordsContext, null);

        return NoContent();
    }

    [HttpGet("{id}/generate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileContentResult))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> Generate(int id)
    {
        _logger.LogDebug("Generate for id={id}", id);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("Generate returning '403 Forbidden'");
            return _403();
        }

        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            _logger.LogDebug("Generate returning '404 Not Found'");
            return _404Order(id);
        }

        var (fileName, xml) = await _indPostGenerator.GenerateXML(id);
        var bytes = Encoding.UTF8.GetBytes(xml);
        return File(bytes, "application/xml", fileName);
    }

    [HttpPost("{id}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> ApproveOrder(int id)
    {
        _logger.LogDebug("ApproveOrder for id={id}", id);

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("ApproveOrder returning '403 Forbidden'");
            return _403();
        }

        var order = await _db.Orders.FindAsync(id);
        if (order == null)
        {
            _logger.LogDebug("ApproveOrder returning '404 Not Found'");
            return _404Order(id);
        }

        order.CheckStatusId = (int)OrderCheckStatusCode.Approved;
        _db.Entry(order).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        _logger.LogDebug("ApproveOrder returning '204 No content' for id={id}", id);
        return NoContent();
    }

    [HttpGet("orderstatus")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<string>> GetOrderStatus(string orderNumber)
    {
        _logger.LogDebug("GetOrderStatus for shk={orderNumber}", orderNumber);

        var statusTitle = await _db.WbrOrders.AsNoTracking()
            .Where(o => o.Shk == orderNumber)
            .Select(o => o.Status.Title)
            .FirstOrDefaultAsync();

        if (statusTitle == null)
        {
            _logger.LogDebug("GetOrderStatus returning '404 Not Found'");
            return _404OrderNumber(orderNumber);
        }

        return Ok(statusTitle);
    }

    [HttpGet("checkstatuses")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<OrderCheckStatus>))]
    public async Task<ActionResult<IEnumerable<OrderCheckStatus>>> GetCheckStatuses()
    {
        var statuses = await _db.CheckStatuses.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        _logger.LogDebug("GetCheckStatuses returning {count} items", statuses.Count);
        return Ok(statuses);
    }

}



