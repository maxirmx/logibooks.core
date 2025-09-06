// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("transportation_types")]
public sealed class TransportationType
{
    [Column("id")]
    public int Id { get; set; }

    [Column("code", TypeName = "numeric(2)")]
    public TransportationTypeCode Code { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("document")]
    public required string Document { get; set; }
}
