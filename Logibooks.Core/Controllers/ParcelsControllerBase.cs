using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
                .ThenByDescending(o => o.Id);
        }
        else
        {
            return query.OrderBy(priorityExpression)
                .ThenBy(o => o.Id);
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
        out bool invalidSort)
    {
        invalidSort = false;

        if (companyId == IRegisterProcessingService.GetWBRId())
        {
            var allowedSortBy = new[] { "id", "statusid", "checkstatusid", "tnved", "shk", "feacnlookup" };
            if (!allowedSortBy.Contains(sortBy.ToLower()))
            {
                invalidSort = true;
                return null;
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

            return query.Cast<BaseParcel>();
        }
        else if (companyId == IRegisterProcessingService.GetOzonId())
        {
            var allowedSortBy = new[] { "id", "statusid", "checkstatusid", "tnved", "postingnumber", "feacnlookup" };
            if (!allowedSortBy.Contains(sortBy.ToLower()))
            {
                invalidSort = true;
                return null;
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

            return query.Cast<BaseParcel>();
        }

        return null;
    }
}
