// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;
using Logibooks.Core.Models;

namespace Logibooks.Core.Controllers;

public abstract class ParcelsControllerBase : LogibooksControllerBase
{
    protected ParcelsControllerBase(IHttpContextAccessor httpContextAccessor, AppDbContext db, ILogger logger)
        : base(httpContextAccessor, db, logger)
    {
    }

    /// <summary>
    /// Calculate match priority for sorting: 1 = best match, 8 = worst match
    /// </summary>
    protected IQueryable<T> ApplyMatchSorting<T>(IQueryable<T> query, string sortOrder) where T : BaseParcel
    {
        Expression<Func<T, int>> priorityExpression = o =>
            // Priority 1: Has keywords with exactly one distinct FeacnCode and it matches TnVed
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() == 1 &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) ? 1 :

            // Priority 2: Has keywords with multiple distinct FeacnCodes and one of them matches TnVed
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() > 1 &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) ? 2 :

            // Priority 3: Has keywords with exactly one distinct FeacnCode, doesn't match TnVed, but TnVed exists in FeacnCodes
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() == 1 &&
            !o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) &&
            _db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 3 :

            // Priority 4: Has keywords with multiple distinct FeacnCodes, none match TnVed, but TnVed exists in FeacnCodes
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() > 1 &&
            !o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) &&
            _db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 4 :

            // Priority 5: Has keywords with exactly one distinct FeacnCode, doesn't match TnVed, and TnVed not in FeacnCodes
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() == 1 &&
            !o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) &&
            !_db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 5 :

            // Priority 6: Has keywords with multiple distinct FeacnCodes, none match TnVed, and TnVed not in FeacnCodes
            o.BaseParcelKeyWords.Any() &&
            o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Select(fc => fc.FeacnCode).Distinct().Count() > 1 &&
            !o.BaseParcelKeyWords.SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes).Any(fc => fc.FeacnCode == o.TnVed) &&
            !_db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 6 :

            // Priority 7: No keywords but TnVed exists in FeacnCodes table
            !o.BaseParcelKeyWords.Any() &&
            _db.FeacnCodes.Any(fc => fc.Code == o.TnVed) ? 7 :

            // Priority 8: No keywords and TnVed not in FeacnCodes table
            8;

        if (sortOrder.ToLower() == "desc")
        {
            return query.OrderByDescending(priorityExpression)
                .ThenBy(o => o.Id); 
        }
        else
        {
            return query.OrderBy(priorityExpression)
                .ThenBy(o => o.Id); // Always use ascending Id as final tiebreaker
        }
    }

    protected IQueryable<BaseParcel>? BuildParcelQuery(
        int companyId,
        int registerId,
        int? statusId,
        int? checkStatusId,
        string? tnVed,
        string sortBy,
        string sortOrder,
        int? minCheckStatusId = null)
    {
        if (!IsValidSortBy(companyId, sortBy))
        {
            return null;
        }
        var filterQuery = BuildParcelFilterQuery(companyId, registerId, statusId, checkStatusId, tnVed, minCheckStatusId);
        var orderedQuery = ApplyParcelOrdering(filterQuery, sortBy, sortOrder);
        return orderedQuery;
    }

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

    protected IQueryable<BaseParcel> BuildParcelFilterQuery(
        int companyId,
        int registerId,
        int? statusId,
        int? checkStatusId,
        string? tnVed,
        int? minCheckStatusId)
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
            if (minCheckStatusId.HasValue)
            {
                query = query.Where(o => o.CheckStatusId >= minCheckStatusId.Value);
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
            if (minCheckStatusId.HasValue)
            {
                query = query.Where(o => o.CheckStatusId >= minCheckStatusId.Value);
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
                query = query.Where(o => o.TnVed != null && o.TnVed.Contains(tnVed));
            }

            return query.Cast<BaseParcel>();
        }
    }

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

    protected async Task<BaseParcel?> GetNextParcelKeysetAsync(
        int companyId,
        int registerId,
        int parcelId,
        int? statusId,
        int? checkStatusId,
        string? tnVed,
        string sortBy,
        string sortOrder,
        int? minCheckStatusId = null)
    {
        // Check if sortBy is valid
        if (!IsValidSortBy(companyId, sortBy))
        {
            return null;
        }
        var filterQuery = BuildParcelFilterQuery(companyId, registerId, statusId, checkStatusId, tnVed, minCheckStatusId);      

        // Get current parcel's key values for keyset pagination
        var currentKeys = await GetCurrentParcelKeysAsync(filterQuery, parcelId, sortBy);
        if (currentKeys == null)
        {
            return null; // Current parcel not found or filtered out
        }

        // Apply keyset predicate to find next parcel
        var keysetQuery = ApplyKeysetPredicate(filterQuery, currentKeys, sortBy, sortOrder);
        
        // Apply ordering and get first result
        var orderedQuery = ApplyParcelOrdering(keysetQuery, sortBy, sortOrder);
        
        return await orderedQuery.FirstOrDefaultAsync();
    }

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

        // Calculate priority using the same logic as ApplyMatchSorting
        int priority = CalculateMatchPriority(currentParcel);
        
        return new ParcelKeys { Id = currentParcel.Id, IntKey = priority };
    }

    private int CalculateMatchPriority(BaseParcel parcel)
    {
        var hasKeywords = parcel.BaseParcelKeyWords.Any();
        if (!hasKeywords)
        {
            // Check if TnVed exists in FeacnCodes table
            var tnVedExists = !string.IsNullOrEmpty(parcel.TnVed) && 
                             _db.FeacnCodes.Any(fc => fc.Code == parcel.TnVed);
            return tnVedExists ? 7 : 8;
        }

        var feacnCodes = parcel.BaseParcelKeyWords
            .SelectMany(kw => kw.KeyWord.KeyWordFeacnCodes)
            .Select(fc => fc.FeacnCode)
            .Distinct()
            .ToList();

        var distinctCount = feacnCodes.Count;
        var matchesTnVed = !string.IsNullOrEmpty(parcel.TnVed) && feacnCodes.Contains(parcel.TnVed);
        var tnVedInDb = !string.IsNullOrEmpty(parcel.TnVed) && 
                        _db.FeacnCodes.Any(fc => fc.Code == parcel.TnVed);

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

    private IQueryable<BaseParcel> ApplyKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, string sortBy, string sortOrder)
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

    private IQueryable<BaseParcel> ApplyStatusIdKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, bool isDescending)
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

    private IQueryable<BaseParcel> ApplyCheckStatusIdKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, bool isDescending)
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

    private IQueryable<BaseParcel> ApplyTnVedKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, bool isDescending)
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

    private IQueryable<WbrParcel> ApplyShkKeysetPredicate(IQueryable<WbrParcel> query, ParcelKeys currentKeys, bool isDescending)
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

    private IQueryable<OzonParcel> ApplyPostingNumberKeysetPredicate(IQueryable<OzonParcel> query, ParcelKeys currentKeys, bool isDescending)
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

    private IQueryable<BaseParcel> ApplyFeacnKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, bool isDescending)
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

    private IQueryable<BaseParcel> ApplyIdKeysetPredicate(IQueryable<BaseParcel> query, ParcelKeys currentKeys, bool isDescending)
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

    private class ParcelKeys
    {
        public int Id { get; set; }
        public int? IntKey { get; set; }
        public string? StringKey { get; set; }
    }
}
