// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Logibooks.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("feacn_codes")]
[Index(nameof(Code), Name = "IX_feacn_codes_code")]
public class FeacnCode
{
    public const int FeacnCodeLength = 10;

    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [StringLength(FeacnCodeLength)]
    public required string Code { get; set; } = string.Empty;

    [Column("code_ex")]
    public required string CodeEx { get; set; } = string.Empty;

    [Column("name")]
    public required string Name { get; set; } = string.Empty;

    [Column("normalized")]
    public required string NormalizedName { get; set; } = string.Empty;

    [Column("from_date")]
    public DateOnly? FromDate { get; set; } = null;

    [Column("to_date")]
    public DateOnly? ToDate { get; set; } = null;

    [Column("old_name")]
    public string? OldName { get; set; } = null;

    [Column("old_name_to_date")]
    public DateOnly? OldNameToDate { get; set; } = null;

    [Column("parent_id")]
    public int? ParentId { get; set; }
    public FeacnCode? Parent { get; set; }

    public ICollection<FeacnCode>? Children { get; set; }

    public static IQueryable<FeacnCode> RoQuery(AppDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        IQueryable<FeacnCode> query = db.FeacnCodes.AsNoTracking()
                   .Where(c => (c.FromDate == null || c.FromDate <= today) &&
                               (c.ToDate == null || c.ToDate > today));
        return query;
    }
}
