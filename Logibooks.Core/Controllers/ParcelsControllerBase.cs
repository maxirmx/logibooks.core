// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;

namespace Logibooks.Core.Controllers;

/// <summary>
/// Base controller providing common parcel query, sorting and keyset pagination helpers.
///
/// This abstract controller contains reusable logic used by concrete parcel controllers
/// (e.g. WBR / Ozon specific controllers) for building filtered queries, applying
/// ordering (including special FEACN match sorting) and performing keyset-style "next"
/// lookups efficiently.
///
/// Notes:
/// - Methods return IQueryable so calling code can add additional projection/filters
///   before execution.
/// - FEACN match sorting uses a computed priority (1..8) where lower values indicate
///   better matches between keywords/FEACN codes and the parcel's TN VED value.
/// - Keyset pagination helpers compute the current row's sort keys and then apply
///   a predicate to fetch the next row in the requested sort order without using
///   OFFSET-based paging.
/// </summary>
public abstract class ParcelsControllerBase(IHttpContextAccessor httpContextAccessor, AppDbContext db, ILogger logger) : 
    LogibooksControllerBase(httpContextAccessor, db, logger)
{
    /// <summary>
    /// Cache for FeacnCode values to avoid N+1 query issues during priority calculations.
    /// This cache is populated once per request and reused for all priority calculations.
    /// </summary>
    private HashSet<string>? _cachedFeacnCodes;

    /// <summary>
    /// Get all FeacnCode values from the database and cache them in memory.
    /// This method populates the cache once per request to avoid repeated database queries
    /// during priority calculations for multiple parcels.
    /// </summary>
    /// <returns>A HashSet containing all FeacnCode values from the database</returns>
    private async Task<HashSet<string>> GetFeacnCodesAsync()
    {
        if (_cachedFeacnCodes == null)
        {
            _cachedFeacnCodes = await _db.FeacnCodes
                .Select(fc => fc.Code)
                .ToHashSetAsync();
        }
        return _cachedFeacnCodes;
    }

    /// <summary>
    /// Calculate match priority for sorting: 1 = best match, 8 = worst match.
    ///
    /// This method builds an expression that computes an integer priority for each
    /// parcel based on the following factors (evaluated in order):
    /// 1) Whether the parcel has associated keyword FEACN codes and how many distinct
    ///    FEACN codes are present for those keywords.
    /// 2) Whether one of those FEACN codes equals the parcel's TnVed value.
    /// 3) Whether the parcel's TnVed exists in the global FeacnCodes table.
    ///
    /// The resulting priority values are used to order parcels by match quality. The
    /// returned IQueryable is ordered by computed priority (ascending = best match)
    /// or descending depending on the requested sort order. Id is always used as a
    /// final stable tiebreaker.
    /// 
    /// Note: This method still uses database queries in the expression tree for SQL translation.
    /// For in-memory calculations, use CalculateMatchPriorityAsync which leverages caching.
    /// </summary>
    protected IQueryable<T> ApplyMatchSorting<T>(IQueryable<T> query, string sortOrder) where T : BaseParcel
    {
        // Build an expression tree that can be translated to SQL by EF Core.
        Expression<Func<T, int>> priorityExpression = o =>
            // Priority 1: Exactly one distinct FEACN code across all keywords and it equals TnVed
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() == 1 &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) ? 1 :

            // Priority 2: Multiple distinct FEACN codes across keywords and one of them equals TnVed
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() > 1 &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) ? 2 :

            // Priority 3: Exactly one distinct FEACN code, it does NOT equal TnVed, but TnVed exists in FeacnCodes table
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() == 1 &&
            !o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) &&
            _db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 3 :

            // Priority 4: Multiple distinct FEACN codes, none equal TnVed, but TnVed exists in FeacnCodes
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() > 1 &&
            !o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) &&
            _db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 4 :

            // Priority 5: Exactly one distinct FEACN code, doesn't match TnVed, and TnVed NOT present in FeacnCodes
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() == 1 &&
            !o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) &&
            !_db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 5 :

            // Priority 6: Multiple distinct FEACN codes, none match TnVed, and TnVed NOT present in FeacnCodes
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() > 1 &&
            !o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) &&
            !_db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 6 :

            // Priority 7: No keywords but TnVed exists in FeacnCodes table
            !o.BaseParcelKeyWords.Any() &&
            _db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 7 :

            // Priority 8: No keywords and TnVed not in FeacnCodes table (worst match)
            8;

        // Apply direction and stable tiebreaker by Id
        if (sortOrder.ToLower() == "desc")
        {
            return query.OrderByDescending(priorityExpression)
                .ThenByDescending(o => o.Id); 
        }
        else
        {
            return query.OrderBy(priorityExpression)
                .ThenBy(o => o.Id); // Always use ascending Id as final tiebreaker
        }
    }

    /// <summary>
    /// Build a full parcel query (filters + ordering) for a given company/register and sorting
    /// parameters. Returns null if the requested sortBy is not valid for the company.
    /// </summary>
    protected IQueryable<BaseParcel>? BuildParcelQuery(
        int companyId,
        int registerId,
        int? statusId,
        int? checkStatusId,
        string? tnVed,
        string sortBy,
        string sortOrder,
        bool withIssues)
    {
        if (!IsValidSortBy(companyId, sortBy))
        {
            return null;
        }
        var filterQuery = BuildParcelFilterQuery(companyId, registerId, statusId, checkStatusId, tnVed, withIssues);
        var orderedQuery = ApplyParcelOrdering(filterQuery, sortBy, sortOrder);
        return orderedQuery;
    }

    /// <summary>
    /// Validate that the provided sortBy field is allowed for the given company type.
    /// This prevents clients from requesting invalid sort fields (e.g. SHK for Ozon).
    /// </summary>
    private static bool IsValidSortBy(int companyId, string sortBy)
    {
        // Determine allowed sortBy based on parcel type
        string[] allowedSortBy;
        if (companyId == IRegisterProcessingService.GetWBRId())
        {
            allowedSortBy = ["id", "statusid", "checkstatusid", "tnved", "shk", "feacnlookup"];
        }
        else /*  (companyId == IRegisterProcessingService.GetOzonId()) */
        {
            allowedSortBy = ["id", "statusid", "checkstatusid", "tnved", "postingnumber", "feacnlookup"];
        }
        return allowedSortBy.Contains(sortBy.ToLower());
    }

    /// <summary>
    /// Build the base IQueryable for parcels for the given company/register applying
    /// includes and basic filters. This method does not apply ordering.
    ///
    /// Includes are added to eagerly load related entities used by sorting/lookup
    /// (keywords, FEACN codes, prefixes etc.) so the resulting projection can be
    /// translated by EF Core into a single query when possible.
    /// </summary>
    protected IQueryable<BaseParcel> BuildParcelFilterQuery(
        int companyId,
        int registerId,
        int? statusId,
        int? checkStatusId,
        string? tnVed,
        bool withIssues)
    {
        if (companyId == IRegisterProcessingService.GetWBRId())
        {
            var query = _db.WbrParcels.AsNoTracking()
                .Include(o => o.BaseParcelStopWords)
                .Include(o => o.BaseParcelKeyWords)
                    .ThenInclude(bkw => bkw.KeyWord)
                        .ThenInclude(kw => kw.KeyWordFeacnCodes)
                .Include(o => o.BaseParcelFeacnPrefixes)
                    .ThenInclude(bofp => bofp.FeacnPrefix)
                        .ThenInclude(fp => fp.FeacnOrder)
                .Where(o => o.RegisterId == registerId && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);

            // Apply filters
            if (withIssues)
            {
                query = query.Where(o => 
                    o.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues &&
                    o.CheckStatusId < (int)ParcelCheckStatusCode.NoIssues);
            }

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
                // Partial match on TN VED (contains) to allow searching by prefix or substring
                query = query.Where(o => o.TnVed != null && o.TnVed.Contains(tnVed));
            }

            return query.Cast<BaseParcel>();
        }
        else /*  (companyId == IRegisterProcessingService.GetOzonId()) */
        {
            var query = _db.OzonParcels.AsNoTracking()
                .Include(o => o.BaseParcelStopWords)
                .Include(o => o.BaseParcelKeyWords)
                    .ThenInclude(bkw => bkw.KeyWord)
                        .ThenInclude(kw => kw.KeyWordFeacnCodes)
                .Include(o => o.BaseParcelFeacnPrefixes)
                    .ThenInclude(bofp => bofp.FeacnPrefix)
                        .ThenInclude(fp => fp.FeacnOrder)
                .Where(o => o.RegisterId == registerId && o.CheckStatusId != (int)ParcelCheckStatusCode.MarkedByPartner);

            // Apply filters
            if (withIssues)
            {
                query = query.Where(o =>
                    o.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues &&
                    o.CheckStatusId < (int)ParcelCheckStatusCode.NoIssues);
            }

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
                // Partial match on TN VED (contains) to allow searching by prefix or substring
                query = query.Where(o => o.TnVed != null && o.TnVed.Contains(tnVed));
            }

            return query.Cast<BaseParcel>();
        }
    }

    /// <summary>
    /// Apply ordering to a filtered parcel query based on sortBy/sortOrder. Supports
    /// simple fields and company-specific columns (SHK / PostingNumber) as well as
    /// the custom FEACN lookup ordering that uses ApplyMatchSorting.
    ///
    /// If the query is empty this method returns a stable ordered query by Id to
    /// allow callers to continue using the returned value safely.
    /// </summary>
    protected IOrderedQueryable<BaseParcel> ApplyParcelOrdering(IQueryable<BaseParcel> query, string sortBy, string sortOrder)
    {
        // Get the first parcel to determine company type for validation
        var firstParcel = query.FirstOrDefault();
        if (firstParcel == null)
        {
            // Return a dummy ordered query for empty result sets
            return query.OrderBy(o => o.Id);
        }

        // Determine allowed sortBy based on parcel type
        string[] allowedSortBy;
        if (firstParcel is WbrParcel)
        {
            allowedSortBy = ["id", "statusid", "checkstatusid", "tnved", "shk", "feacnlookup"];
        }
        else if (firstParcel is OzonParcel)
        {
            allowedSortBy = ["id", "statusid", "checkstatusid", "tnved", "postingnumber", "feacnlookup"];
        }
        else
        {
            // Fallback to basic sorting
            allowedSortBy = ["id"];
        }

        if (!allowedSortBy.Contains(sortBy.ToLower()))
        {
            return query.OrderBy(o => o.Id); // Return default ordering for invalid sortBy
        }

        // Map sortBy + sortOrder to specific OrderBy calls. Use Id as final tiebreaker
        return (sortBy.ToLower(), sortOrder.ToLower()) switch
        {
            ("statusid", "asc") => query.OrderBy(o => o.StatusId).ThenBy(o => o.Id),
            ("statusid", "desc") => query.OrderByDescending(o => o.StatusId).ThenBy(o => o.Id),
            ("checkstatusid", "asc") => query.OrderBy(o => o.CheckStatusId).ThenBy(o => o.Id),
            ("checkstatusid", "desc") => query.OrderByDescending(o => o.CheckStatusId).ThenBy(o => o.Id),
            ("tnved", "asc") => query.OrderBy(o => o.TnVed).ThenBy(o => o.Id),
            ("tnved", "desc") => query.OrderByDescending(o => o.TnVed).ThenBy(o => o.Id),
            ("shk", "asc") when firstParcel is WbrParcel => (IOrderedQueryable<BaseParcel>)query.Cast<WbrParcel>().OrderBy(o => o.Shk).ThenBy(o => o.Id).Cast<BaseParcel>(),
            ("shk", "desc") when firstParcel is WbrParcel => (IOrderedQueryable<BaseParcel>)query.Cast<WbrParcel>().OrderByDescending(o => o.Shk).ThenBy(o => o.Id).Cast<BaseParcel>(),
            ("postingnumber", "asc") when firstParcel is OzonParcel => (IOrderedQueryable<BaseParcel>)query.Cast<OzonParcel>().OrderBy(o => o.PostingNumber).ThenBy(o => o.Id).Cast<BaseParcel>(),
            ("postingnumber", "desc") when firstParcel is OzonParcel => (IOrderedQueryable<BaseParcel>)query.Cast<OzonParcel>().OrderByDescending(o => o.PostingNumber).ThenBy(o => o.Id).Cast<BaseParcel>(),
            ("feacnlookup", "desc") => (IOrderedQueryable<BaseParcel>)ApplyMatchSorting(query, "desc"),
            ("feacnlookup", _) => (IOrderedQueryable<BaseParcel>)ApplyMatchSorting(query, "asc"),
            ("id", "desc") => query.OrderByDescending(o => o.Id),
            _ => query.OrderBy(o => o.Id)
        };
    }

    /// <summary>
    /// Find the next parcel for keyset pagination given the current parcel id and
    /// desired sorting. Returns null when the requested sortBy is invalid.
    ///
    /// The method first attempts to find the next parcel using keyset pagination.
    /// If the current parcel is not found in the filtered set, it finds the next parcel
    /// that comes after the current parcel in sort order AND matches the applied filters.
    /// </summary>
    protected async Task<BaseParcel?> GetNextParcelKeysetAsync(
        int companyId,
        int registerId,
        int parcelId,
        int? statusId,
        int? checkStatusId,
        string? tnVed,
        string sortBy,
        string sortOrder,
        bool withIssues)
    {
        // Check if sortBy is valid
        if (!IsValidSortBy(companyId, sortBy))
        {
            return null;
        }
        var filterQuery = BuildParcelFilterQuery(companyId, registerId, statusId, checkStatusId, tnVed, withIssues);      

        // Get current parcel's key values for keyset pagination
        var currentKeys = await GetCurrentParcelKeysAsync(filterQuery, parcelId, sortBy);
        if (currentKeys == null)
        {
            // Current parcel not found in filtered set - need to find next item after current parcel
            // that matches the filters, not just the first filtered item
            return await GetNextFilteredParcelAfterCurrentAsync(companyId, registerId, parcelId, statusId, checkStatusId, tnVed, sortBy, sortOrder, withIssues);
        }

        // Apply keyset predicate to find next parcel
        var keysetQuery = ApplyKeysetPredicate(filterQuery, currentKeys, sortBy, sortOrder);
        
        // Apply ordering and get first result
        var orderedQuery2 = ApplyParcelOrdering(keysetQuery, sortBy, sortOrder);
        
        return await orderedQuery2.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Find the next parcel that comes after the current parcel in sort order AND matches the applied filters.
    /// This is used when the current parcel is filtered out (e.g., doesn't have issues when withIssues=true).
    /// </summary>
    private async Task<BaseParcel?> GetNextFilteredParcelAfterCurrentAsync(
        int companyId,
        int registerId,
        int parcelId,
        int? statusId,
        int? checkStatusId,
        string? tnVed,
        string sortBy,
        string sortOrder,
        bool withIssues)
    {
        // Build query without the specific filters (withIssues, statusId, checkStatusId, tnVed)
        // to get the current parcel's key values
        var unfiltered = BuildParcelFilterQuery(companyId, registerId, null, null, null, false);
        
        // Get current parcel's keys from the unfiltered set
        var currentKeys = await GetCurrentParcelKeysAsync(unfiltered, parcelId, sortBy);
        if (currentKeys == null)
        {
            // Current parcel doesn't exist at all - return first filtered item
            var filterQuery = BuildParcelFilterQuery(companyId, registerId, statusId, checkStatusId, tnVed, withIssues);
            var orderedQuery = ApplyParcelOrdering(filterQuery, sortBy, sortOrder);
            return await orderedQuery.FirstOrDefaultAsync();
        }

        // Apply keyset predicate to unfiltered query to get all parcels after current
        var afterCurrentQuery = ApplyKeysetPredicate(unfiltered, currentKeys, sortBy, sortOrder);
        
        // Now apply the filters to get only the parcels after current that match filters
        var filteredAfterCurrent = ApplyFiltersToQuery(afterCurrentQuery, statusId, checkStatusId, tnVed, withIssues);
        
        // Apply ordering and get first result
        var orderedQuery2 = ApplyParcelOrdering(filteredAfterCurrent, sortBy, sortOrder);
        
        return await orderedQuery2.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Apply the specific filters (statusId, checkStatusId, tnVed, withIssues) to a query.
    /// This is used to filter parcels that come after the current parcel.
    /// </summary>
    private static IQueryable<BaseParcel> ApplyFiltersToQuery(
        IQueryable<BaseParcel> query,
        int? statusId,
        int? checkStatusId,
        string? tnVed,
        bool withIssues)
    {
        // Apply withIssues filter
        if (withIssues)
        {
            query = query.Where(o => 
                o.CheckStatusId >= (int)ParcelCheckStatusCode.HasIssues &&
                o.CheckStatusId < (int)ParcelCheckStatusCode.NoIssues);
        }

        // Apply statusId filter
        if (statusId != null)
        {
            query = query.Where(o => o.StatusId == statusId);
        }

        // Apply checkStatusId filter
        if (checkStatusId != null)
        {
            query = query.Where(o => o.CheckStatusId == checkStatusId);
        }

        // Apply tnVed filter
        if (!string.IsNullOrWhiteSpace(tnVed))
        {
            query = query.Where(o => o.TnVed != null && o.TnVed.Contains(tnVed));
        }

        return query;
    }

    /// <summary>
    /// Calculate priority (1..8) for an individual parcel instance. This mirrors the
    /// expression used in ApplyMatchSorting but runs in-memory against the loaded
    /// parcel entity; used when computing key values for keyset pagination.
    /// 
    /// This method now uses cached FeacnCodes to avoid N+1 query issues when processing
    /// multiple parcels.
    /// </summary>
    private async Task<int> CalculateMatchPriorityAsync(BaseParcel parcel)
    {
        var hasKeywords = parcel.BaseParcelKeyWords.Any();
        var feacnCodes = await GetFeacnCodesAsync();
        
        if (!hasKeywords)
        {
            // If no keywords exist, determine whether the parcel's TnVed exists in the
            // cached FeacnCodes. If it does, prefer it (priority 7) over unknown (8).
            var tnVedExists = !string.IsNullOrEmpty(parcel.TnVed) && 
                             feacnCodes.Contains(parcel.TnVed);
            return tnVedExists ? 7 : 8;
        }

        // Collect distinct FEACN codes referenced by keywords for this parcel
        var parcelFeacnCodes = parcel.BaseParcelKeyWords
            .SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes)
            .Select(fc => fc.FeacnCode)
            .Distinct()
            .ToList();

        var distinctCount = parcelFeacnCodes.Count;
        var matchesTnVed = !string.IsNullOrEmpty(parcel.TnVed) && parcelFeacnCodes.Contains(parcel.TnVed);
        var tnVedInDb = !string.IsNullOrEmpty(parcel.TnVed) && 
                        feacnCodes.Contains(parcel.TnVed);

        // Map tuple of (distinctCount, matchesTnVed, tnVedInDb) to priority according to rules
        return (distinctCount, matchesTnVed, tnVedInDb) switch
        {
            (1, true, _) => 1,
            (> 1, true, _) => 2,
            (1, false, true) => 3,
            (> 1, false, true) => 4,
            (1, false, false) => 5,
            (> 1, false, false) => 6,
            _ => 8
        };
    }

    /// <summary>
    /// Retrieve the sort key(s) for the specified parcel depending on the requested
    /// sort field. For string-based fields the StringKey is used; for integer-based
    /// fields the IntKey is used. For feacnlookup the priority calculation is performed.
    /// </summary>
    private async Task<ParcelKeys?> GetCurrentParcelKeysAsync(IQueryable<BaseParcel> filterQuery, int parcelId, string sortBy)
    {
        return sortBy.ToLower() switch
        {
            "statusid" => await filterQuery.Where(p => p.Id == parcelId)
                .Select(p => new ParcelKeys { Id = p.Id, IntKey = p.StatusId })
                .FirstOrDefaultAsync(),
            "checkstatusid" => await filterQuery.Where(p => p.Id == parcelId)
                .Select(p => new ParcelKeys { Id = p.Id, IntKey = p.CheckStatusId })
                .FirstOrDefaultAsync(),
            "tnved" => await filterQuery.Where(p => p.Id == parcelId)
                .Select(p => new ParcelKeys { Id = p.Id, StringKey = p.TnVed })
                .FirstOrDefaultAsync(),
            "shk" => await filterQuery.Cast<WbrParcel>().Where(p => p.Id == parcelId)
                .Select(p => new ParcelKeys { Id = p.Id, StringKey = p.Shk })
                .FirstOrDefaultAsync(),
            "postingnumber" => await filterQuery.Cast<OzonParcel>().Where(p => p.Id == parcelId)
                .Select(p => new ParcelKeys { Id = p.Id, StringKey = p.PostingNumber })
                .FirstOrDefaultAsync(),
            "feacnlookup" => await GetFeacnLookupKeysAsync(filterQuery, parcelId),
            _ => await filterQuery.Where(p => p.Id == parcelId)
                .Select(p => new ParcelKeys { Id = p.Id })
                .FirstOrDefaultAsync()
        };
    }

    /// <summary>
    /// For FEACN lookup ordering compute the match priority for the current parcel
    /// and return it in IntKey so keyset predicate logic can operate on the numeric
    /// priority value.
    /// </summary>
    private async Task<ParcelKeys?> GetFeacnLookupKeysAsync(IQueryable<BaseParcel> filterQuery, int parcelId)
    {
        // For feacnlookup, we need to calculate the priority for the current parcel
        var currentParcel = await filterQuery.Where(p => p.Id == parcelId)
            .Include(o => o.BaseParcelKeyWords)
                .ThenInclude(bkw => bkw.KeyWord)
                    .ThenInclude(kw => kw.KeyWordFeacnCodes)
            .FirstOrDefaultAsync();

        if (currentParcel == null)
            return null;

        // Calculate priority using the cached approach to avoid N+1 queries
        int priority = await CalculateMatchPriorityAsync(currentParcel);
        
        return new ParcelKeys { Id = currentParcel.Id, IntKey = priority };
    }

    /// <summary>
    /// Given the current parcel keys, return a query representing all rows that come
    /// after the current row according to the requested sortBy/sortOrder (keyset predicate).
    /// </summary>
    private static IQueryable<BaseParcel> ApplyKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, string sortBy, string sortOrder)
    {
        var isDescending = sortOrder.ToLower() == "desc";
        
        return sortBy.ToLower() switch
        {
            "statusid" => ApplyStatusIdKeysetPredicate(query, currentKeys, isDescending),
            "checkstatusid" => ApplyCheckStatusIdKeysetPredicate(query, currentKeys, isDescending),
            "tnved" => ApplyTnVedKeysetPredicate(query, currentKeys, isDescending),
            "shk" => ApplyShkKeysetPredicate(query.Cast<WbrParcel>(), currentKeys, isDescending).Cast<BaseParcel>(),
            "postingnumber" => ApplyPostingNumberKeysetPredicate(query.Cast<OzonParcel>(), currentKeys, isDescending).Cast<BaseParcel>(),
            "feacnlookup" => ApplyFeacnKeysetPredicate(query, currentKeys, isDescending),
            _ => ApplyIdKeysetPredicate(query, currentKeys, isDescending)
        };
    }

    /// <summary>
    /// Keyset predicate for StatusId ordering. When ascending we want StatusId > cur
    /// or same StatusId and Id greater than current Id. For descending the comparison
    /// operator is inverted but Id tie-breaker uses > so we iterate in the correct
    /// direction for keyset pagination.
    /// </summary>
    private static IQueryable<BaseParcel> ApplyStatusIdKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, bool isDescending)
    {
        var currentStatusId = currentKeys.IntKey ?? 0;
        var currentId = currentKeys.Id;

        if (isDescending)
        {
            return query.Where(p => 
                p.StatusId < currentStatusId ||
                (p.StatusId == currentStatusId && p.Id > currentId));
        }
        else
        {
            return query.Where(p => 
                p.StatusId > currentStatusId ||
                (p.StatusId == currentStatusId && p.Id > currentId));
        }
    }

    /// <summary>
    /// Keyset predicate for CheckStatusId ordering. Same tie-breaker rules as StatusId.
    /// </summary>
    private static IQueryable<BaseParcel> ApplyCheckStatusIdKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, bool isDescending)
    {
        var currentCheckStatusId = currentKeys.IntKey ?? 0;
        var currentId = currentKeys.Id;

        if (isDescending)
        {
            return query.Where(p => 
                p.CheckStatusId < currentCheckStatusId ||
                (p.CheckStatusId == currentCheckStatusId && p.Id > currentId));
        }
        else
        {
            return query.Where(p => 
                p.CheckStatusId > currentCheckStatusId ||
                (p.CheckStatusId == currentCheckStatusId && p.Id > currentId));
        }
    }

    /// <summary>
    /// Keyset predicate for TN VED string ordering using string.Compare to ensure
    /// culture-insensitive ordinal comparison behavior in SQL translation.
    /// </summary>
    private static IQueryable<BaseParcel> ApplyTnVedKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, bool isDescending)
    {
        var currentTnVed = currentKeys.StringKey;
        var currentId = currentKeys.Id;

        if (isDescending)
        {
            return query.Where(p =>
                string.Compare(p.TnVed, currentTnVed) < 0 ||
                (p.TnVed == currentTnVed && p.Id > currentId));
        }
        else
        {
            return query.Where(p =>
                string.Compare(p.TnVed, currentTnVed) > 0 ||
                (p.TnVed == currentTnVed && p.Id > currentId));
        }
    }

    /// <summary>
    /// Keyset predicate for SHK (WBR-specific) ordering. Operates on WbrParcel queries.
    /// </summary>
    private static IQueryable<WbrParcel> ApplyShkKeysetPredicate(IQueryable<WbrParcel> query, ParcelKeys currentKeys, bool isDescending)
    {
        var currentShk = currentKeys.StringKey;
        var currentId = currentKeys.Id;

        if (isDescending)
        {
            return query.Where(p =>
                string.Compare(p.Shk, currentShk) < 0 ||
                (p.Shk == currentShk && p.Id > currentId));
        }
        else
        {
            return query.Where(p =>
                string.Compare(p.Shk, currentShk) > 0 ||
                (p.Shk == currentShk && p.Id > currentId));
        }
    }

    /// <summary>
    /// Keyset predicate for PostingNumber (Ozon-specific) ordering. Operates on OzonParcel queries.
    /// </summary>
    private static IQueryable<OzonParcel> ApplyPostingNumberKeysetPredicate(IQueryable<OzonParcel> query, ParcelKeys currentKeys, bool isDescending)
    {
        var currentPostingNumber = currentKeys.StringKey;
        var currentId = currentKeys.Id;

        if (isDescending)
        {
            return query.Where(p =>
                string.Compare(p.PostingNumber, currentPostingNumber) < 0 ||
                (p.PostingNumber == currentPostingNumber && p.Id > currentId));
        }
        else
        {
            return query.Where(p =>
                string.Compare(p.PostingNumber, currentPostingNumber) > 0 ||
                (p.PostingNumber == currentPostingNumber && p.Id > currentId));
        }
    }

    /// <summary>
    /// Keyset predicate for FEACN lookup ordering. Currently this method performs a
    /// simple id-based predicate as a placeholder: for FEACN ordering we compute a
    /// numeric priority but full cross-row comparison by priority then id would
    /// require translating the same expression used during ordering. To keep the
    /// logic simple and efficient we fall back to id-based progression after
    /// computing the priority for the current row.
    ///
    /// NOTE: This means keyset pagination across FEACN-priority boundaries is driven
    /// primarily by Id; it is intentional to avoid complex SQL expressions that are
    /// hard to keep consistent between computing priority and applying keyset predicate.
    /// </summary>
    private static IQueryable<BaseParcel> ApplyFeacnKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, bool isDescending)
    {
        var currentPriority = currentKeys.IntKey ?? 8;
        var currentId = currentKeys.Id;

        if (isDescending)
        {
            // For descending priority, we want higher priority values (worse matches) first
            return query.Where(p => p.Id > currentId); // Simple id-based pagination for feacnlookup desc
        }
        else
        {
            // For ascending priority, we want lower priority values (better matches) first
            return query.Where(p => p.Id > currentId); // Simple id-based pagination for feacnlookup asc
        }
    }

    /// <summary>
    /// Default keyset predicate for Id ordering. Uses greater/less-than comparisons
    /// depending on requested direction.
    /// </summary>
    private static IQueryable<BaseParcel> ApplyIdKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, bool isDescending)
    {
        var currentId = currentKeys.Id;

        if (isDescending)
        {
            return query.Where(p => p.Id < currentId);
        }
        else
        {
            return query.Where(p => p.Id > currentId);
        }
    }

    /// <summary>
    /// Helper type used to carry the current row's key values required for keyset
    /// predicate evaluation. IntKey is used for numeric sort fields; StringKey for
    /// string-based sorts. Id is always included for tie-breakers.
    /// </summary>
    private class ParcelKeys
    {
        public int Id { get; set; }
        public int? IntKey { get; set; }
        public string? StringKey { get; set; }
    }
}
