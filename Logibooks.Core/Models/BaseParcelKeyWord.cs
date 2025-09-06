// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Models;

[Table("base_parcel_key_words")]
[Index(nameof(BaseParcelId), Name = "IX_base_parcel_key_words_base_parcel_id")]
[Index(nameof(KeyWordId), Name = "IX_base_parcel_key_words_key_word_id")]
[Index(nameof(BaseParcelId), nameof(KeyWordId), Name = "IX_base_parcel_key_words_base_parcel_id_key_word_id")]
public class BaseParcelKeyWord
{
    [Column("base_parcel_id")]
    public int BaseParcelId { get; set; }
    public BaseParcel BaseParcel { get; set; } = null!;

    [Column("key_word_id")]
    public int KeyWordId { get; set; }
    public KeyWord KeyWord { get; set; } = null!;
}