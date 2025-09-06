// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("feacn_prefixes")]
public class FeacnPrefix
{
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [StringLength(FeacnCode.FeacnCodeLength)]
    public required string Code { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("interval_code")]
    public string? IntervalCode { get; set; }

    [Column("feacn_order_id")]
    public int? FeacnOrderId { get; set; }
    public FeacnOrder? FeacnOrder { get; set; }

    public ICollection<FeacnPrefixException> FeacnPrefixExceptions { get; set; } = [];
    public ICollection<BaseParcelFeacnPrefix> BaseParcelFeacnPrefixes { get; set; } = [];

    [NotMapped]
    public long LeftValue
    {
        get
        {
            if (Code != null)
            {
                if (long.TryParse(Code.PadRight(FeacnCode.FeacnCodeLength, '0'), out var result))
                {
                    return result;
                }
            }
            return 0;
        }
    }

    [NotMapped]
    public long RightValue
    {
        get
        {
            if (IntervalCode != null)
            {
                if (long.TryParse(IntervalCode.PadRight(FeacnCode.FeacnCodeLength, '0'), out var result))
                {
                    return result;
                }
            }
            return 0;
        }
    }
}
