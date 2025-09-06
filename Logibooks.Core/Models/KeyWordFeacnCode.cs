// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Logibooks.Core.Models;

[Table("key_word_feacn_codes")]
[Index(nameof(FeacnCode), Name = "IX_key_word_feacn_codes_feacn_code")]
[Index(nameof(KeyWordId), Name = "IX_key_word_feacn_codes_key_word_id")]
public class KeyWordFeacnCode
{
    [Column("key_word_id")]
    public int KeyWordId { get; set; }
    public KeyWord KeyWord { get; set; } = null!;

    [Column("feacn_code")]
    [StringLength(Models.FeacnCode.FeacnCodeLength, MinimumLength = Models.FeacnCode.FeacnCodeLength, ErrorMessage = "Код ТН ВЭД должен состоять из 10 цифр")]
    public string FeacnCode { get; set; } = string.Empty;
}