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

using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("feacn_prefixes")]
public class FeacnPrefix
{
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public required string Code { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("interval_code")]
    public string? IntervalCode { get; set; }

    [Column("feacn_order_id")]
    public int FeacnOrderId { get; set; }
    public FeacnOrder FeacnOrder { get; set; } = null!;

    public ICollection<FeacnPrefixException> FeacnPrefixExceptions { get; set; } = new List<FeacnPrefixException>();
    public ICollection<BaseOrderFeacnPrefix> BaseOrderFeacnPrefixes { get; set; } = new List<BaseOrderFeacnPrefix>();

    [NotMapped]
    public long LeftValue
    {
        get
        {
            if (Code != null)
            {
                if (long.TryParse(Code.PadRight(10, '0'), out var result))
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
                if (long.TryParse(IntervalCode.PadRight(10, '0'), out var result))
                {
                    return result;
                }
            }
            return 0;
        }
    }
}
