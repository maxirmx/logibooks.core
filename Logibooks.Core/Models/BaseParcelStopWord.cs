// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("base_parcel_stop_words")]
public class BaseParcelStopWord
{
    [Column("base_parcel_id")]
    public int BaseParcelId { get; set; }
    public BaseParcel BaseParcel { get; set; } = null!;

    [Column("stop_word_id")]
    public int StopWordId { get; set; }
    public StopWord StopWord { get; set; } = null!;

}