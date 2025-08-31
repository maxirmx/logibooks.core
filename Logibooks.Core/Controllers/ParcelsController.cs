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
using System.Linq.Expressions;
using System.Text;

using Logibooks.Core.Authorization;
using Logibooks.Core.Data;
using Logibooks.Core.Extensions;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]

public class ParcelsController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    ILogger<ParcelsController> logger,
    IMapper mapper,
    IParcelValidationService validationService,
    IParcelFeacnCodeLookupService feacnLookupService,
    IMorphologySearchService morphologyService,
    IRegisterProcessingService processingService,
    IParcelIndPostGenerator indPostGenerator) : LogibooksControllerBase(httpContextAccessor, db, logger)
{
    private const int MaxPageSize = 1000;
    private readonly IUserInformationService _userService = userService;
    private readonly IMapper _mapper = mapper;
    private readonly IParcelValidationService _validationService = validationService;
    private readonly IParcelFeacnCodeLookupService _feacnLookupService = feacnLookupService;
    private readonly IMorphologySearchService _morphologyService = morphologyService;
    private readonly IRegisterProcessingService _processingService = processingService;
    private readonly IParcelIndPostGenerator _indPostGenerator = indPostGenerator;

    /// <summary>
    /// Calculate match priority for sorting: 1 = best match, 6 = worst match
    /// (1) Parcels with keywords that have exactly one matching FeacnCode for parcel TnVed
    /// (2) Parcels with keywords that have multiple matching FeacnCodes for parcel TnVed
    /// (3) Parcels with keywords but exactly one FeacnCode (not matching parcel TnVed)
    /// (4) Parcels with keywords but multiple FeacnCodes (not matching parcel TnVed)
    /// (5) Parcels without keywords but TnVed exists in FeacnCodes
    /// (6) Parcels without keywords and TnVed not in FeacnCodes
    /// </summary>
    private IQueryable<T> ApplyMatchSorting<T>(IQueryable<T> query, string sortOrder) where T : BaseParcel
    {
        // Define the priority calculation expression once
        Expression<Func<T, int>> priorityExpression = o => 
            // Priority 1: Has keywords with exactly one matching FeacnCode for TnVed
            o.BaseParcelKeyWords.Any() && 
            o.BaseParcelKeyWords.Any(kw => kw.KeyWord.KeyWordFeacnCodes.Any(fc => fc.FeacnCode == o.TnVed)) &&
            o.BaseParcelKeyWords.Where(kw => kw.KeyWord.KeyWordFeacnCodes.Any(fc => fc.FeacnCode == o.TnVed))
                .All(kw => kw.KeyWord.KeyWordFeacnCodes.Count() == 1) ? 1 :
            
            // Priority 2: Has keywords with multiple matching FeacnCodes for TnVed
            o.BaseParcelKeyWords.Any() && 
            o.BaseParcelKeyWords.Any(kw => kw.KeyWord.KeyWordFeacnCodes.Any(fc => fc.FeacnCode == o.TnVed)) &&
            o.BaseParcelKeyWords.Where(kw => kw.KeyWord.KeyWordFeacnCodes.Any(fc => fc.FeacnCode == o.TnVed))
                .Any(kw => kw.KeyWord.KeyWordFeacnCodes.Count() > 1) ? 2 :
            
            // Priority 3: Has keywords but exactly one FeacnCode (not matching TnVed)
            o.BaseParcelKeyWords.Any() && 
            !o.BaseParcelKeyWords.Any(kw => kw.KeyWord.KeyWordFeacnCodes.Any(fc => fc.FeacnCode == o.TnVed)) &&
            o.BaseParcelKeyWords.All(kw => kw.KeyWord.KeyWordFeacnCodes.Count() == 1) ? 3 :
            
            // Priority 4: Has keywords but multiple FeacnCodes (not matching TnVed)
            o.BaseParcelKeyWords.Any() && 
            !o.BaseParcelKeyWords.Any(kw => kw.KeyWord.KeyWordFeacnCodes.Any(fc => fc.FeacnCode == o.TnVed)) &&
            o.BaseParcelKeyWords.Any(kw => kw.KeyWord.KeyWordFeacnCodes.Count() > 1) ? 4 :
            
            // Priority 5: No keywords but TnVed exists in FeacnCodes table
            !o.BaseParcelKeyWords.Any() && 
            _db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 5 :
            
            // Priority 6: No keywords and TnVed not in FeacnCodes table
            6;

        if (sortOrder.ToLower() == "desc")
        {
            return query.OrderByDescending(priorityExpression)
                .ThenByDescending(o => o.Id);
        }
        else
        {
            return query.OrderBy(priorityExpression)
                .ThenBy(o => o.Id);
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ParcelViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<ParcelViewItem>> GetOrder(int id)
    {
        _logger.LogDebug("GetOrder for id={id}", id);

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetOrder returning '403 Forbidden'");
            return _403();
        }

        // First, get the order with its register to determine the company type
        var orderWithRegister = await _db.Parcels.AsNoTracking()
            .Include(o => o.Register)
            .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);

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
        BaseParcel? order = null;
        int companyId = orderWithRegister.Register.CompanyId;

        if (companyId == IRegisterProcessingService.GetWBRId())
        {
            order = await _db.WbrParcels.AsNoTracking()
                .Include(o => o.Register)
                .Include(o => o.BaseParcelStopWords)
                .Include(o => o.BaseParcelKeyWords)
                .Include(o => o.BaseParcelFeacnPrefixes)
                    .ThenInclude(bofp => bofp.FeacnPrefix)
                        .ThenInclude(fp => fp.FeacnOrder)
                .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);
        }
        else if (companyId == IRegisterProcessingService.GetOzonId())
        {
            order = await _db.OzonParcels.AsNoTracking()
                .Include(o => o.Register)
                .Include(o => o.BaseParcelStopWords)
                .Include(o => o.BaseParcelKeyWords)
                .Include(o => o.BaseParcelFeacnPrefixes)
                    .ThenInclude(bofp => bofp.FeacnPrefix)
                        .ThenInclude(fp => fp.FeacnOrder)
                .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);
        }

        if (order == null)
        {
            _logger.LogDebug("GetOrder returning '404 Not Found' - order not found in specific table");
            return _404Order(id);
        }

        var lastView = await _db.ParcelViews
            .Where(v => v.UserId == _curUserId && v.BaseOrderId == order.Id)
            .OrderByDescending(v => v.DTime)
            .FirstOrDefaultAsync();

        var viewItem = new ParcelViewItem(order)
        {
            DTime = lastView?.DTime
        };

        _logger.LogDebug("GetOrder returning {orderType} order for companyId={cid}",
            order.GetType().Name, companyId);

        return viewItem;
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateOrder(int id, ParcelUpdateItem update)
    {
        _logger.LogDebug("UpdateOrder for id={id}", id);

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("UpdateOrder returning '403 Forbidden'");
            return _403();
        }

        // First, get the order with its register to determine the company type
        var orderWithRegister = await _db.Parcels
            .Include(o => o.Register)
            .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);

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
        BaseParcel? order = null;
        int companyId = orderWithRegister.Register.CompanyId;

        if (companyId == IRegisterProcessingService.GetWBRId())
        {
            order = await _db.WbrParcels
                .Include(o => o.Register)
                .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);
        }
        else if (companyId == IRegisterProcessingService.GetOzonId())
        {
            order = await _db.OzonParcels
                .Include(o => o.Register)
                .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);
        }

        if (order == null)
        {
            _logger.LogDebug("UpdateOrder returning '404 Not Found' - order not found in specific table");
            return _404Order(id);
        }

        if (order is WbrParcel wbr)
        {
            wbr.UpdateFrom(update, _mapper);
        }
        else if (order is OzonParcel ozon)
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
        var orderWithRegister = await _db.Parcels
            .Include(o => o.Register)
            .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);

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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResult<ParcelViewItem>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<PagedResult<ParcelViewItem>>> GetParcels(
        int registerId,
        int? statusId = null,
        int? checkStatusId = null,
        string? tnVed = null,
        int page = 1,
        int pageSize = 100,
        string? sortBy = null,
        string sortOrder = "asc")
    {
        _logger.LogDebug("GetParcels for register={reg} status={st} checkStatus={cs} tnVed={tnVed} page={page} size={size} sortBy={sortBy} sortOrder={sortOrder}",
            registerId, statusId, checkStatusId, tnVed, page, pageSize, sortBy, sortOrder);

        if (page <= 0 ||
            (pageSize != -1 && (pageSize <= 0 || pageSize > MaxPageSize)))
        {
            _logger.LogDebug("GetParcels returning '400 Bad Request' - invalid pagination");
            return _400();
        }

        sortBy ??= "id";
        sortOrder = string.IsNullOrEmpty(sortOrder) ? "asc" : sortOrder.ToLower();

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetParcels returning '403 Forbidden'");
            return _403();
        }

        // First determine the register type
        var register = await _db.Registers.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == registerId);
        
        if (register == null)
        {
            _logger.LogDebug("GetParcels returning '404 Not Found' - register not found");
            return _404Register(registerId);
        }

        List<BaseParcel> items;
        int totalCount;
        int actualPage;
        int actualPageSize;
        int totalPages;

        // Create a typed query based on the register company type
        if (register.CompanyId == IRegisterProcessingService.GetWBRId())
        {
            var allowedSortBy = new[] { "id", "statusid", "checkstatusid", "tnved", "shk", "feacnlookup" };
            if (!allowedSortBy.Contains(sortBy.ToLower()))
            {
                _logger.LogDebug("GetOrders returning '400 Bad Request' - invalid sortBy for WBR");
                return _400();
            }

            var query = _db.WbrParcels.AsNoTracking()
                .Include(o => o.BaseParcelStopWords)
                .Include(o => o.BaseParcelKeyWords)
                    .ThenInclude(bkw => bkw.KeyWord)
                        .ThenInclude(kw => kw.KeyWordFeacnCodes)
                .Include(o => o.BaseParcelFeacnPrefixes)
                    .ThenInclude(bofp => bofp.FeacnPrefix)
                        .ThenInclude(fp => fp.FeacnOrder)
                .Where(o => o.RegisterId == registerId && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);

            if (statusId != null)
            {
                query = query.Where(o => o.StatusId == statusId);
            }

            if (checkStatusId != null)
            {
                query = query.Where(o => o.CheckStatusId == checkStatusId);
            }

            if (!string.IsNullOrWhiteSpace(tnVed))
            {
                query = query.Where(o => o.TnVed != null && o.TnVed.Contains(tnVed));
            }

            query = (sortBy.ToLower(), sortOrder) switch
            {
                ("statusid", "asc") => query.OrderBy(o => o.StatusId),
                ("statusid", "desc") => query.OrderByDescending(o => o.StatusId),
                ("checkstatusid", "asc") => query.OrderBy(o => o.CheckStatusId),
                ("checkstatusid", "desc") => query.OrderByDescending(o => o.CheckStatusId),
                ("tnved", "asc") => query.OrderBy(o => o.TnVed),
                ("tnved", "desc") => query.OrderByDescending(o => o.TnVed),
                ("shk", "asc") => query.OrderBy(o => o.Shk), 
                ("shk", "desc") => query.OrderByDescending(o => o.Shk),
                ("feacnlookup", _) => ApplyMatchSorting(query, sortOrder),
                ("id", "desc") => query.OrderByDescending(o => o.Id),
                _ => query.OrderBy(o => o.Id)
            };

            totalCount = await query.CountAsync();

            actualPage = pageSize == -1 ? 1 : page;
            actualPageSize = pageSize == -1 ? (totalCount == 0 ? 1 : totalCount) : pageSize;
            totalPages = (int)Math.Ceiling(totalCount / (double)actualPageSize);

            if (actualPage > totalPages && totalPages > 0)
            {
                actualPage = 1;
            }

            items = (await query
                .Skip((actualPage - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync()).Cast<BaseParcel>().ToList();
        }
        else if (register.CompanyId == IRegisterProcessingService.GetOzonId())
        {
            // Use Ozon-specific allowed sort fields
            var allowedSortBy = new[] { "id", "statusid", "checkstatusid", "tnved", "postingnumber", "feacnlookup" };
            if (!allowedSortBy.Contains(sortBy.ToLower()))
            {
                _logger.LogDebug("GetOrders returning '400 Bad Request' - invalid sortBy for Ozon");
                return _400();
            }

            var query = _db.OzonParcels.AsNoTracking()
                .Include(o => o.BaseParcelStopWords)
                .Include(o => o.BaseParcelKeyWords)
                    .ThenInclude(bkw => bkw.KeyWord)
                        .ThenInclude(kw => kw.KeyWordFeacnCodes)
                .Include(o => o.BaseParcelFeacnPrefixes)
                    .ThenInclude(bofp => bofp.FeacnPrefix)
                        .ThenInclude(fp => fp.FeacnOrder)
                .Where(o => o.RegisterId == registerId && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);

            if (statusId != null)
            {
                query = query.Where(o => o.StatusId == statusId);
            }

            if (checkStatusId != null)
            {
                query = query.Where(o => o.CheckStatusId == checkStatusId);
            }

            if (!string.IsNullOrWhiteSpace(tnVed))
            {
                query = query.Where(o => o.TnVed != null && o.TnVed.Contains(tnVed));
            }

            query = (sortBy.ToLower(), sortOrder) switch
            {
                ("statusid", "asc") => query.OrderBy(o => o.StatusId),
                ("statusid", "desc") => query.OrderByDescending(o => o.StatusId),
                ("checkstatusid", "asc") => query.OrderBy(o => o.CheckStatusId),
                ("checkstatusid", "desc") => query.OrderByDescending(o => o.CheckStatusId),
                ("tnved", "asc") => query.OrderBy(o => o.TnVed),
                ("tnved", "desc") => query.OrderByDescending(o => o.TnVed),
                ("postingnumber", "asc") => query.OrderBy(o => o.PostingNumber), 
                ("postingnumber", "desc") => query.OrderByDescending(o => o.PostingNumber),
                ("feacnlookup", _) => ApplyMatchSorting(query, sortOrder),
                ("id", "desc") => query.OrderByDescending(o => o.Id),
                _ => query.OrderBy(o => o.Id)
            };

            totalCount = await query.CountAsync();

            actualPage = pageSize == -1 ? 1 : page;
            actualPageSize = pageSize == -1 ? (totalCount == 0 ? 1 : totalCount) : pageSize;
            totalPages = (int)Math.Ceiling(totalCount / (double)actualPageSize);

            if (actualPage > totalPages && totalPages > 0)
            {
                actualPage = 1;
            }

            items = (await query
                .Skip((actualPage - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync()).Cast<BaseParcel>().ToList();
        }
        else
        {
            // For non-WBR, non-Ozon registers, return error
            _logger.LogDebug("GetParcels returning '400 Bad Request' - unsupported register company type");
            return _400CompanyId(register.CompanyId);
        }

        var viewItems = items.Select(o => new ParcelViewItem(o)).ToList();

        var result = new PagedResult<ParcelViewItem>
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

        _logger.LogDebug("GetParcels returning {count} items", items.Count);
        return Ok(result);
    }

    [HttpPost("{id}/lookup-feacn-code")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LookupFeacnCodeResult))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<LookupFeacnCodeResult>> LookupFeacnCode(int id)
    {
        _logger.LogDebug("LookupFeacnCode for id={id}", id);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("LookupFeacnCode returning '403 Forbidden'");
            return _403();
        }

        var parcel = await _db.Parcels
            .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);
        if (parcel == null)
        {
            _logger.LogDebug("LookupFeacnCode returning '404 Not Found'");
            return _404Order(id);
        }

        var keyWords = await _db.KeyWords.AsNoTracking().ToListAsync();
        var morphologyContext = _morphologyService.InitializeContext(
            keyWords.Where(k => k.MatchTypeId >= (int)WordMatchTypeCode.MorphologyMatchTypes)
                .Select(k => new StopWord { Id = k.Id, Word = k.Word, MatchTypeId = k.MatchTypeId }));
        var wordsLookupContext = new WordsLookupContext<KeyWord>(
            keyWords.Where(k => k.MatchTypeId < (int)WordMatchTypeCode.MorphologyMatchTypes));

        var keyWordIds = await _feacnLookupService.LookupAsync(parcel, morphologyContext, wordsLookupContext);
        return Ok(new LookupFeacnCodeResult { KeyWordIds = keyWordIds });
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

        var order = await _db.Parcels
            .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);
        if (order == null)
        {
            _logger.LogDebug("ValidateOrder returning '404 Not Found'");
            return _404Order(id);
        }

        var stopWords = await _db.StopWords.AsNoTracking().ToListAsync();
        var morphologyContext = _morphologyService.InitializeContext(
            stopWords.Where(sw => sw.MatchTypeId >= (int)WordMatchTypeCode.MorphologyMatchTypes));
        var wordsLookupContext = new WordsLookupContext<StopWord>(
            stopWords.Where(sw => sw.MatchTypeId < (int)WordMatchTypeCode.MorphologyMatchTypes));

        await _validationService.ValidateAsync(order, morphologyContext, wordsLookupContext, null);

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

        var order = await _db.Parcels.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);
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
    public async Task<IActionResult> ApproveParcel(int id, [FromQuery] bool withExcise = false)
    {
        _logger.LogDebug("ApproveParcel for id={id}, withExcise={withExcise}", id, withExcise);

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("ApproveParcel returning '403 Forbidden'");
            return _403();
        }

        var parcel = await _db.Parcels
            .FirstOrDefaultAsync(o => o.Id == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);
        if (parcel == null)
        {
            _logger.LogDebug("ApproveParcel returning '404 Not Found'");
            return _404Order(id);
        }

        parcel.CheckStatusId = withExcise 
            ? (int)ParcelCheckStatusCode.ApprovedWithExcise 
            : (int)ParcelCheckStatusCode.Approved;
        
        _db.Entry(parcel).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        _logger.LogDebug("ApproveParcel returning '204 No content' for id={id}, status={status}", 
            id, withExcise ? "ApprovedWithExcise" : "Approved");
        return NoContent();
    }

    [HttpGet("orderstatus")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<string>> GetOrderStatus(string orderNumber)
    {
        _logger.LogDebug("GetOrderStatus for shk={orderNumber}", orderNumber);

        var statusTitle = await _db.WbrParcels.AsNoTracking()
            .Where(o => o.Shk == orderNumber && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner)
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<ParcelCheckStatus>))]
    public async Task<ActionResult<IEnumerable<ParcelCheckStatus>>> GetCheckStatuses()
    {
        var statuses = await _db.CheckStatuses.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        _logger.LogDebug("GetCheckStatuses returning {count} items", statuses.Count);
        return Ok(statuses);
    }

}

