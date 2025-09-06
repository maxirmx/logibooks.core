// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Logibooks.Core.Models;

[Table("parcel_views")]
[Index(nameof(BaseParcelId), nameof(UserId), nameof(DTime),  Name = "IX_parcel_views_baseparcelid_userid_dtime")]
public class ParcelView
{
    [Column("id")]
    public int Id { get; set; }

    [Column("dtime")]
    public DateTime DTime { get; set; } = DateTime.UtcNow;

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    [JsonIgnore]
    public User User { get; set; } = null!;

    [Column("base_parcel_id")]
    public int BaseParcelId { get; set; }

    [ForeignKey("BaseParcelId")]
    [JsonIgnore]
    public BaseParcel BaseParcel { get; set; } = null!;
}