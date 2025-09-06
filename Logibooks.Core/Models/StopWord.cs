// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("stop_words")]
[Index(nameof(Word), IsUnique = true, Name = "IX_stop_words_word")]
public class StopWord : WordBase
{
    public ICollection<BaseParcelStopWord> BaseOrderStopWords { get; set; } = [];
}
