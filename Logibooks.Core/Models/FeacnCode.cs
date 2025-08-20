// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

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
    [StringLength(FeacnCodeLength)]
    public required string CodeEx { get; set; } = string.Empty;

    [Column("name")]
    public required string Description { get; set; } = string.Empty;

    [Column("normalized")]
    public required string DescriptionEx { get; set; } = string.Empty;

    [Column("from_date")]
    public DateOnly? FromDate { get; set; } = null;

    [Column("to_date")]
    public DateOnly? ToDate { get; set; } = null;

    [Column("old_name")]
    public string? OldName { get; set; } = null;

    [Column("old_name_to_date")]
    public DateOnly? OldNameToDate { get; set; } = null;

    [ForeignKey("Parent")]
    [Column("parent_id")]
    public int? ParentId { get; set; }
    public FeacnCode? Parent { get; set; }

    public ICollection<FeacnCode>? Children { get; set; }
}
