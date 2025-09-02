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

using Logibooks.Core.Authorization;
using Logibooks.Core.Constants;
using Logibooks.Core.Data;
using Logibooks.Core.Extensions;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpCompress.Archives;
using System.Linq.Expressions;
using System.Linq;

namespace Logibooks.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
public class RegistersController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    IUserInformationService userService,
    ILogger<RegistersController> logger,
    IRegisterValidationService validationService,
    IRegisterFeacnCodeLookupService feacnLookupService,
    IRegisterProcessingService processingService,
    IParcelIndPostGenerator indPostGenerator) : ParcelsControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userService = userService;

    private readonly string[] allowedSortBy = [
        "id", 
        "filename", 
        "date", 
        "parcelstotal", 
        "senderrecepient", 
        "countries",
        "invoice", 
        "dealnumber"
    ];

    private readonly int maxPageSize = 100;
    private readonly IRegisterValidationService _validationService = validationService;
    private readonly IRegisterFeacnCodeLookupService _feacnLookupService = feacnLookupService;
    private readonly IRegisterProcessingService _processingService = processingService;
    private readonly IParcelIndPostGenerator _indPostGenerator = indPostGenerator;

    private static Expression<Func<Register, string>> PartySortSelector(bool byRecipient)
    {
        return r =>
            r.CustomsProcedure != null && r.CustomsProcedure.Code == 10
                ? (byRecipient
                    ? (r.TheOtherCompany != null ? (r.TheOtherCompany.ShortName ?? string.Empty) : string.Empty)
                    : (r.Company != null ? (r.Company.ShortName ?? string.Empty) : string.Empty))
                : (byRecipient
                    ? (r.Company != null ? (r.Company.ShortName ?? string.Empty) : string.Empty)
                    : (r.TheOtherCompany != null ? (r.TheOtherCompany.ShortName ?? string.Empty) : string.Empty));
    }

    private static Expression<Func<Register, string>> CountrySortSelector(bool byDestination)
    {
        return r =>
            r.CustomsProcedure != null && r.CustomsProcedure.Code == 10
                ? (byDestination
                    ? (r.TheOtherCountryCode == null
                        ? string.Empty
                        : r.TheOtherCountryCode == CountryConstants.RussiaIsoNumeric
                            ? CountryConstants.RussiaNameRuShort
                            : (r.TheOtherCountry != null ? (r.TheOtherCountry.NameRuShort ?? string.Empty) : string.Empty))
                    : CountryConstants.RussiaNameRuShort)
                : (byDestination
                    ? CountryConstants.RussiaNameRuShort
                    : (r.TheOtherCountryCode == null
                        ? string.Empty
                        : r.TheOtherCountryCode == CountryConstants.RussiaIsoNumeric
                            ? CountryConstants.RussiaNameRuShort
                            : (r.TheOtherCountry != null ? (r.TheOtherCountry.NameRuShort ?? string.Empty) : string.Empty)));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RegisterViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<RegisterViewItem>> GetRegister(int id)
    {
        _logger.LogDebug("GetRegister for id={id}", id);

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetRegister returning '403 Forbidden'");
            return _403();
        }

        var register = await _db.Registers
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (register == null)
        {
            _logger.LogDebug("GetRegister returning '404 Not Found'");
            return _404Register(id);
        }

        var parcelsStats = await FetchOrdersStatsAsync([id]);
        var parcelsByStatus = parcelsStats.GetValueOrDefault(id, []);
        var placesTotal = await FetchUniqueIdentifiersCountAsync([id]);

        var view = register.ToViewItem(parcelsByStatus, placesTotal[id]);

        _logger.LogDebug("GetRegister returning register with {count} parcels", view.ParcelsTotal);
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

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("GetRegisters returning '403 Forbidden'");
            return _403();
        }

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

        IQueryable<Register> baseQuery = _db.Registers
            .Include(r => r.Company)
            .Include(r => r.TheOtherCompany)
            .Include(r => r.TheOtherCountry)
            .Include(r => r.TransportationType)
            .Include(r => r.CustomsProcedure)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {           
            if (!CountryConstants.RussiaNameRuShort.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = baseQuery.Where(r => 
                       EF.Functions.Like(r.FileName, $"%{search}%")
                    || (r.InvoiceNumber != null && EF.Functions.Like(r.InvoiceNumber, $"%{search}%"))
                    || (r.DealNumber != null && EF.Functions.Like(r.DealNumber, $"%{search}%"))
                    || (r.Company != null && EF.Functions.Like(r.Company.ShortName, $"%{search}%"))
                    || (r.TheOtherCompany != null && EF.Functions.Like(r.TheOtherCompany.ShortName, $"%{search}%"))
                    || (r.TheOtherCountry != null && EF.Functions.Like(r.TheOtherCountry.NameRuShort, $"%{search}%"))
                    || (r.TransportationType != null && EF.Functions.Like(r.TransportationType.Name, $"%{search}%"))
                    || (r.CustomsProcedure != null && EF.Functions.Like(r.CustomsProcedure.Name, $"%{search}%"))
                );
            }
        }

        IQueryable<Register> query = baseQuery;

        query = (sortBy.ToLower(), sortOrder) switch
        {
            ("filename", "asc") => query.OrderBy(r => r.FileName),
            ("filename", "desc") => query.OrderByDescending(r => r.FileName),
            ("date", "asc") => query.OrderBy(r => r.DTime),
            ("date", "desc") => query.OrderByDescending(r => r.DTime),
            ("dealnumber", "asc") => query.OrderBy(r => r.DealNumber),
            ("dealnumber", "desc") => query.OrderByDescending(r => r.DealNumber),
            ("parcelstotal", "asc") => query.OrderBy(r => r.Orders.Count()),
            ("parcelstotal", "desc") => query.OrderByDescending(r => r.Orders.Count()),
            ("companies", "asc") => query.OrderBy(PartySortSelector(false)),
            ("companies", "desc") => query.OrderByDescending(PartySortSelector(false)),
            ("countries", "asc") => query.OrderBy(r => r.CustomsProcedure != null ? r.CustomsProcedure.Name : string.Empty).ThenBy(CountrySortSelector(false)),
            ("countries", "desc") => query.OrderByDescending(r => r.CustomsProcedure != null ? r.CustomsProcedure.Name : string.Empty).ThenByDescending(CountrySortSelector(false)),
            ("invoice", "asc") => query.OrderBy(r => r.TransportationType != null ? r.TransportationType.Document : string.Empty).ThenBy(r => r.InvoiceNumber).ThenBy(r => r.InvoiceDate),
            ("invoice", "desc") => query.OrderByDescending(r => r.TransportationType != null ? r.TransportationType.Document : string.Empty).ThenByDescending(r => r.InvoiceNumber).ThenByDescending(r => r.InvoiceDate),
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
        var placesTotal = await FetchUniqueIdentifiersCountAsync(ids);

        var viewItems = items.Select(r => r.ToViewItem(stats.GetValueOrDefault(r.Id, []), placesTotal[r.Id])).ToList();

        var result = new PagedResult<RegisterViewItem>
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
            Sorting = new SortingInfo { SortBy = sortBy, SortOrder = sortOrder },
            Search = search
        };

        _logger.LogDebug("GetRegisters returning count: {count} items", viewItems.Count);
        return Ok(result);
    }

    [HttpPost("upload/{companyId?}")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UploadRegister(IFormFile file, int? companyId = null)
    {
        _logger.LogDebug("UploadRegister called for {name} ({size} bytes)", file?.FileName, file?.Length);

        int cId = companyId ?? IRegisterProcessingService.GetWBRId();

        var ok = await _userService.CheckLogist(_curUserId);
        if (!ok)
        {
            _logger.LogDebug("UploadRegister returning '403 Forbidden'");
            return _403();
        }

        if (cId != IRegisterProcessingService.GetWBRId() && cId != IRegisterProcessingService.GetOzonId())
        {
            _logger.LogDebug($"Unknown company id: {cId}", cId);
            return _400CompanyId((int)cId);
        }

        if (file == null || file.Length == 0)
        {
            _logger.LogDebug("UploadRegister returning '400 Bad Request' - empty file");
            return _400EmptyRegister();
        }

        // Get the file extension
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

        // Handle based on file type
        if (fileExtension == ".xlsx" || fileExtension == ".xls")
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            byte[] excelContent = ms.ToArray();
            try
            {
                var reference = await _processingService.UploadRegisterFromExcelAsync(cId, excelContent, file.FileName);
                return CreatedAtAction(nameof(UploadRegister), new { id = reference.Id }, reference);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError("Mapping file not found at {path}, UploadRegister returning '500 Internal Server Error'", ex.FileName);
                return _500Mapping(ex.FileName ?? string.Empty);
            }
            catch (InvalidOperationException)
            {
                _logger.LogDebug("UploadRegister returning '400 Bad Request' - Excel file is empty");
                return _400EmptyRegister();
            }
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

            try
            {
                var reference = await _processingService.UploadRegisterFromExcelAsync(cId, excelContent, excelFileName);
                _logger.LogDebug("UploadRegister processed archive with Excel");
                return CreatedAtAction(nameof(UploadRegister), new { id = reference.Id }, reference);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError("Mapping file not found at {path}, UploadRegister returning '500 Internal Server Error'", ex.FileName);
                return _500Mapping(ex.FileName ?? string.Empty);
            }
            catch (InvalidOperationException)
            {
                _logger.LogDebug("UploadRegister returning '400 Bad Request' - Excel file is empty");
                return _400EmptyRegister();
            }
        }
        else
        {
            _logger.LogDebug("UploadRegister returning '400 Bad Request' - unsupported file type {ext}", fileExtension);
            return _400UnsupportedFileType(fileExtension);
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateRegister(int id, RegisterUpdateItem update)
    {
        _logger.LogDebug("UpdateRegister for id={id}", id);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("UpdateRegister returning '403 Forbidden'");
            return _403();
        }

        var register = await _db.Registers.FindAsync(id);
        if (register == null)
        {
            _logger.LogDebug("UpdateRegister returning '404 Not Found'");
            return _404Register(id);
        }

        if (update.TheOtherCountryCode != null && update.TheOtherCountryCode != 0 &&
            !await _db.Countries.AsNoTracking().AnyAsync(c => c.IsoNumeric == update.TheOtherCountryCode))
        {
            _logger.LogDebug("UpdateRegister returning '404 Not Found' - country");
            return _404Object(update.TheOtherCountryCode.Value);
        }

        if (update.TransportationTypeId != null && update.TransportationTypeId != 0 &&
            !await _db.TransportationTypes.AsNoTracking().AnyAsync(t => t.Id == update.TransportationTypeId))
        {
            _logger.LogDebug("UpdateRegister returning '404 Not Found' - transportation type");
            return _404Object(update.TransportationTypeId.Value);
        }

        if (update.CustomsProcedureId != null && update.CustomsProcedureId != 0 &&
            !await _db.CustomsProcedures.AsNoTracking().AnyAsync(c => c.Id == update.CustomsProcedureId))
        {
            _logger.LogDebug("UpdateRegister returning '404 Not Found' - customs procedure");
            return _404Object(update.CustomsProcedureId.Value);
        }

        if (update.TheOtherCompanyId != null && update.TheOtherCompanyId != 0 &&
            !await _db.Companies.AsNoTracking().AnyAsync(c => c.Id == update.TheOtherCompanyId))
        {
            _logger.LogDebug("UpdateRegister returning '404 Not Found' - company");
            return _404Object(update.TheOtherCompanyId.Value);
        }

        register.ApplyUpdateFrom(update);

        _db.Entry(register).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        _logger.LogDebug("UpdateRegister updated register {id}", id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteRegister(int id)
    {
        _logger.LogDebug("DeleteRegister for id={id}", id);

        var ok = await _userService.CheckLogist(_curUserId);
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

    [HttpPut("{id}/setparcelstatuses/{statusId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> SetParcelStatuses(int id, int statusId)
    {
        _logger.LogDebug("SetParcelStatuses for registerId={id} statusId={statusId}", id, statusId);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("SetParcelStatuses returning '403 Forbidden'");
            return _403();
        }

        if (!await _db.Registers.AnyAsync(r => r.Id == id))
        {
            _logger.LogDebug("SetParcelStatuses returning '404 Not Found' - register");
            return _404Register(id);
        }

        if (!await _db.Statuses.AnyAsync(s => s.Id == statusId))
        {
            _logger.LogDebug("SetParcelStatuses returning '404 Not Found' - status");
            return _404Status(statusId);
        }

        // Update orders in memory instead of using ExecuteUpdateAsync
        var parcelsToUpdate = await _db.Parcels
            .Where(o => o.RegisterId == id && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner)
            .ToListAsync();
        foreach (var parcel in parcelsToUpdate)
        {
            parcel.StatusId = statusId;
        }
        await _db.SaveChangesAsync();

        _logger.LogDebug("SetParcelStatuses updated register {id}", id);
        return NoContent();
    }

    [HttpGet("{id}/download")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileContentResult))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DownloadRegister(int id)
    {
        _logger.LogDebug("DownloadRegister for id={id}", id);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("DownloadRegister returning '403 Forbidden'");
            return _403();
        }

        var register = await _db.Registers.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (register == null)
        {
            _logger.LogDebug("DownloadRegister returning '404 Not Found'");
            return _404Register(id);
        }

        var bytes = await _processingService.DownloadRegisterToExcelAsync(id);
        var fileName = register.FileName;

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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

        var register = await _db.Registers.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (register == null)
        {
            _logger.LogDebug("Generate returning '404 Not Found'");
            return _404Register(id);
        }

        var (fileName, archive) = await _indPostGenerator.GenerateXML4R(id);
        return File(archive, "application/zip", fileName);
    }

    [HttpGet("nextparcel/{parcelId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ParcelViewItem))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<ParcelViewItem>> NextParcel(
        int parcelId,
        int? statusId = null,
        int? checkStatusId = null,
        string? tnVed = null,
        string? sortBy = null,
        string sortOrder = "asc")
    {
        _logger.LogDebug("NextParcel for parcelId={parcelId}", parcelId);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("NextParcel returning '403 Forbidden'");
            return _403();
        }

        var current = await _db.Parcels.AsNoTracking()
            .Include(o => o.Register)
            .FirstOrDefaultAsync(o => o.Id == parcelId);

        if (current == null)
        {
            _logger.LogDebug("NextParcel returning '404 Not Found' - parcel");
            return _404Order(parcelId);
        }

        int registerId = current.RegisterId;
        int companyId = current.Register.CompanyId;

        sortBy ??= "id";
        sortOrder = string.IsNullOrEmpty(sortOrder) ? "asc" : sortOrder.ToLower();

        bool invalidSort;
        var query = BuildParcelQuery(companyId, registerId, statusId, checkStatusId, tnVed, sortBy, sortOrder, out invalidSort);
        if (query == null)
        {
            if (invalidSort)
            {
                _logger.LogDebug("NextParcel returning '400 Bad Request' - invalid sortBy");
                return _400();
            }

            _logger.LogDebug("NextParcel returning '400 Bad Request' - unsupported register company type");
            return _400CompanyId(companyId);
        }

        query = query.Where(o => o.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues);

        var parcels = await query.ToListAsync();
        var index = parcels.FindIndex(o => o.Id == parcelId);
        if (index == -1 || index + 1 >= parcels.Count)
        {
            _logger.LogDebug("NextParcel returning '204 No Content'");
            return NoContent();
        }

        var parcel = parcels[index + 1];

        _logger.LogDebug("NextParcel returning order {id}", parcel.Id);
        return new ParcelViewItem(parcel);
    }

    [HttpGet("the-nextparcel/{parcelId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ParcelViewItem))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<ParcelViewItem>> TheNextParcel(
        int parcelId,
        int? statusId = null,
        int? checkStatusId = null,
        string? tnVed = null,
        string? sortBy = null,
        string sortOrder = "asc")
    {
        _logger.LogDebug("TheNextParcel for parcelId={parcelId}", parcelId);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("TheNextParcel returning '403 Forbidden'");
            return _403();
        }

        var current = await _db.Parcels.AsNoTracking()
            .Include(o => o.Register)
            .FirstOrDefaultAsync(o => o.Id == parcelId);

        if (current == null)
        {
            _logger.LogDebug("TheNextParcel returning '404 Not Found'");
            return _404Order(parcelId);
        }

        int registerId = current.RegisterId;
        int companyId = current.Register.CompanyId;

        sortBy ??= "id";
        sortOrder = string.IsNullOrEmpty(sortOrder) ? "asc" : sortOrder.ToLower();

        bool invalidSort;
        var query = BuildParcelQuery(companyId, registerId, statusId, checkStatusId, tnVed, sortBy, sortOrder, out invalidSort);
        if (query == null)
        {
            if (invalidSort)
            {
                _logger.LogDebug("TheNextParcel returning '400 Bad Request' - invalid sortBy");
                return _400();
            }

            _logger.LogDebug("TheNextParcel returning '400 Bad Request' - unsupported register company type");
            return _400CompanyId(companyId);
        }

        var parcels = await query.ToListAsync();
        var index = parcels.FindIndex(o => o.Id == parcelId);
        if (index == -1 || index + 1 >= parcels.Count)
        {
            _logger.LogDebug("TheNextParcel returning '204 No Content' - no next parcel found");
            return NoContent();
        }

        var parcel = parcels[index + 1];

        _logger.LogDebug("TheNextParcel returning order {id}", parcel.Id);
        return new ParcelViewItem(parcel);
    }

    [HttpPost("{id}/lookup-feacn-codes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GuidReference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<GuidReference>> LookupFeacnCodes(int id)
    {
        _logger.LogDebug("LookupFeacnCodes for id={id}", id);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("LookupFeacnCodes returning '403 Forbidden'");
            return _403();
        }

        if (!await _db.Registers.AnyAsync(r => r.Id == id))
        {
            _logger.LogDebug("LookupFeacnCodes returning '404 Not Found'");
            return _404Register(id);
        }

        var handle = await _feacnLookupService.StartLookupAsync(id);
        return Ok(new GuidReference { Id = handle });
    }

    [HttpGet("lookup-feacn-codes/{handleId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ValidationProgress))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<ValidationProgress>> GetLookupFeacnCodesProgress(Guid handleId)
    {
        _logger.LogDebug("GetLookupFeacnCodesProgress for handle={handle}", handleId);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("GetLookupFeacnCodesProgress returning '403 Forbidden'");
            return _403();
        }

        var progress = _feacnLookupService.GetProgress(handleId);
        if (progress == null)
        {
            _logger.LogDebug("GetLookupFeacnCodesProgress returning '404 Not Found'");
            return _404Handle(handleId);
        }

        return Ok(progress);
    }

    [HttpDelete("lookup-feacn-codes/{handleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> CancelLookupFeacnCodes(Guid handleId)
    {
        _logger.LogDebug("CancelLookupFeacnCodes for handle={handle}", handleId);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("CancelLookupFeacnCodes returning '403 Forbidden'");
            return _403();
        }

        var ok = _feacnLookupService.Cancel(handleId);
        if (!ok)
        {
            _logger.LogDebug("CancelLookupFeacnCodes returning '404 Not Found'");
            return _404Handle(handleId);
        }

        return NoContent();
    }

    [HttpPost("{id}/validate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GuidReference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<GuidReference>> ValidateRegister(int id)
    {
        _logger.LogDebug("ValidateRegister for id={id}", id);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("ValidateRegister returning '403 Forbidden'");
            return _403();
        }

        if (!await _db.Registers.AnyAsync(r => r.Id == id))
        {
            _logger.LogDebug("ValidateRegister returning '404 Not Found'");
            return _404Register(id);
        }

        var handle = await _validationService.StartValidationAsync(id);
        return Ok(new GuidReference { Id = handle });
    }

    [HttpGet("validate/{handleId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ValidationProgress))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<ValidationProgress>> GetValidationProgress(Guid handleId)
    {
        _logger.LogDebug("GetValidationProgress for handle={handle}", handleId);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("GetValidationProgress returning '403 Forbidden'");
            return _403();
        }

        var progress = _validationService.GetProgress(handleId);
        if (progress == null)
        {
            _logger.LogDebug("GetValidationProgress returning '404 Not Found'");
            return _404Handle(handleId);
        }

        return Ok(progress);
    }

    [HttpDelete("validate/{handleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> CancelValidation(Guid handleId)
    {
        _logger.LogDebug("CancelValidation for handle={handle}", handleId);

        if (!await _userService.CheckLogist(_curUserId))
        {
            _logger.LogDebug("CancelValidation returning '403 Forbidden'");
            return _403();
        }

        var ok = _validationService.Cancel(handleId);
        if (!ok)
        {
            _logger.LogDebug("CancelValidation returning '404 Not Found'");
            return _404Handle(handleId);
        }

        return NoContent();
    }

    private async Task<Dictionary<int, Dictionary<int, int>>> FetchOrdersStatsAsync(IEnumerable<int> registerIds)
    {
        var grouped = await _db.Parcels
            .AsNoTracking()
            .Where(o => registerIds.Contains(o.RegisterId))
            .GroupBy(o => new { o.RegisterId, o.CheckStatusId })
            .Select(g => new { g.Key.RegisterId, g.Key.CheckStatusId, Count = g.Count() })
            .ToListAsync();

        return grouped
            .GroupBy(g => g.RegisterId)
            .ToDictionary(
                g => g.Key,
                g => g.Where(x => x.Count > 0).ToDictionary(x => x.CheckStatusId, x => x.Count));
    }

    private async Task<Dictionary<int, int>> FetchUniqueIdentifiersCountAsync(IEnumerable<int> registerIds)
    {
        var registerIdsList = registerIds.ToList();
        var result = new Dictionary<int, int>();

        if (registerIdsList.Count == 0)
        {
            return result;
        }

        // Single query to get both WBR and Ozon unique identifier counts
        // Uses a union to combine both register types in one database call
        var combinedCounts = await (
            // WBR registers - unique SHK counts
            from r in _db.Registers
            join o in _db.WbrParcels on r.Id equals o.RegisterId
            where registerIdsList.Contains(r.Id) && 
                  r.CompanyId == IRegisterProcessingService.GetWBRId() && 
                  o.Shk != null
            group o.Shk by r.Id into g
            select new { RegisterId = g.Key, UniqueCount = g.Distinct().Count() }
        ).Union(
            // Ozon registers - unique PostingNumber counts  
            from r in _db.Registers
            join o in _db.OzonParcels on r.Id equals o.RegisterId
            where registerIdsList.Contains(r.Id) && 
                  r.CompanyId == IRegisterProcessingService.GetOzonId() && 
                  o.PostingNumber != null
            group o.PostingNumber by r.Id into g
            select new { RegisterId = g.Key, UniqueCount = g.Distinct().Count() }
        ).ToListAsync();

        // Populate result dictionary
        foreach (var count in combinedCounts)
        {
            result[count.RegisterId] = count.UniqueCount;
        }

        // Ensure all requested register IDs are present in result (with 0 for registers with no unique identifiers)
        foreach (var registerId in registerIdsList)
        {
            if (!result.ContainsKey(registerId))
            {
                result[registerId] = 0;
            }
        }

        return result;
    }

}
