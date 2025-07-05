using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.Extensions;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class OrdersController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<OrdersController> logger,
    IMapper mapper) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private const int MaxPageSize = 1000;
    private readonly IMapper _mapper = mapper;

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<OrderViewItem>> GetOrder(int id)
    {
        _logger.LogDebug("GetOrder for id={id}", id);

        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetOrder returning '403 Forbidden'");
            return _403();
        }

        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            _logger.LogDebug("GetOrder returning '404 Not Found'");
            return _404Order(id);
        }

        _logger.LogDebug("GetOrder returning order");
        return new OrderViewItem(order);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateOrder(int id, OrderUpdateItem update)
    {
        _logger.LogDebug("UpdateOrder for id={id}", id);

        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("UpdateOrder returning '403 Forbidden'");
            return _403();
        }

        var order = await _db.Orders.FindAsync(id);
        if (order == null)
        {
            _logger.LogDebug("UpdateOrder returning '404 Not Found'");
            return _404Order(id);
        }

        // Use AutoMapper via extension method
        order.UpdateFrom(update, _mapper);

        _db.Entry(order).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        _logger.LogDebug("UpdateOrder returning '204 No content'");
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

        var ok = await _db.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetOrders returning '403 Forbidden'");
            return _403();
        }

        IQueryable<Order> query = _db.Orders.AsNoTracking().Where(o => o.RegisterId == registerId);

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
}
