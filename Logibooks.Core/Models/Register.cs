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
using System.Text.Json.Serialization;

namespace Logibooks.Core.Models;

[Table("registers")]
public class Register
{
    [Column("id")]
    public int Id { get; set; }

    [Column("filename")]
    public required string FileName { get; set; }

    [Column("dtime")]
    public DateTime DTime { get; set; } = DateTime.UtcNow;

    [Column("company_id")]
    public int CompanyId { get; set; }

    [JsonIgnore]
    public Company? Company { get; set; }

    [Column("invoice_number")]
    public string? InvoiceNumber { get; set; }

    [Column("invoice_date")]
    public DateOnly? InvoiceDate { get; set; }

    [Column("dest_country_iso_numeric")]
    public short? DestinationCountryIsoNumeric { get; set; }

    [JsonIgnore]
    public Country? DestinationCountry { get; set; }

    [Column("transportation_type_id")]
    public int TransportationTypeId { get; set; } = 1;
    // { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа" }
    [JsonIgnore]
    public TransportationType? TransportationType { get; set; }

    [Column("customs_procedure_id")]
    public int CustomsProcedureId { get; set; } = 1;
    // { Id = 1, Code = 10, Name = "Экспорт" }
    [JsonIgnore]
    public CustomsProcedure? CustomsProcedure { get; set; }

    [JsonIgnore]
    public ICollection<BaseOrder> Orders { get; set; } = new List<BaseOrder>();
}
