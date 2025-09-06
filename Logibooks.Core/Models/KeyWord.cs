// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("key_words")]
[Index(nameof(Word), IsUnique = true, Name = "IX_key_words_word")]
public class KeyWord : WordBase
{
    public ICollection<BaseParcelKeyWord> BaseParcelKeyWords { get; set; } = [];
    public ICollection<KeyWordFeacnCode> KeyWordFeacnCodes { get; set; } = [];
}
