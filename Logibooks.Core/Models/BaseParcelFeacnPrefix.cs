// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("base_parcel_feacn_prefixes")]
public class BaseParcelFeacnPrefix
{
    [Column("base_parcel_id")]
    public int BaseParcelId { get; set; }
    public BaseParcel BaseParcel { get; set; } = null!;

    [Column("feacn_prefix_id")]
    public int FeacnPrefixId { get; set; }
    public FeacnPrefix FeacnPrefix { get; set; } = null!;
}
