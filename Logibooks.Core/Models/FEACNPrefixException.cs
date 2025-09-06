// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("feacn_prefix_exceptions")]
public class FeacnPrefixException
{
    [Column("id")]
    public int Id { get; set; }

    
   [Column("code")]
   [StringLength(FeacnCode.FeacnCodeLength)]
   public required string Code { get; set; } = string.Empty;

   [Column("feacn_prefix_id")]
   public int FeacnPrefixId { get; set; }
   public FeacnPrefix FeacnPrefix { get; set; } = null!;
}
