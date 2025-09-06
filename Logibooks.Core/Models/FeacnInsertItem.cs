// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("feacn_insert_items")]
[Index(nameof(Code), Name = "IX_insert_items_code", IsUnique = true)]
public class FeacnInsertItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    [Required]
    [StringLength(FeacnCode.FeacnCodeLength)]
    public string Code { get; set; } = string.Empty;

    [Column("insert_before")]
    public string? InsertBefore { get; set; } = null;

    [Column("insert_after")]
    public string? InsertAfter { get; set; } = null;
}
