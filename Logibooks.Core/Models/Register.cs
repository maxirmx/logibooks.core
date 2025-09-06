// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Logibooks.Core.Models;

[Table("registers")]
public class Register
{
    // ...................................................................................................
    // Â ýòîì êëàññå ðåàëèçîâíà áîëüøàÿ âîëîñàòàÿ èìèòàöèÿ óíèâåðñàëüíîñòè, êîòîðóþ õî÷åò çàêàç÷èê.
    // Íà ñàìîì äåëå îíî óìååò ðàáîòàòü â äâóõ ðåæèìàõ
    // Ýêñïîðò 
    //   Òîãäà "Ñòðàíà îòïðàâëåíèÿ": RU
    //         "Ñòðàíà íàçíà÷åíèÿ": îïðåäåëÿåòñÿ ïî ïîëþ TheOtherCountryCode
    //         "Îòïðàâèòåëü": Company [îïðåäåëåíà â ìîìåíò çàãðóçêè ðååñòðà è íå ìîæåò áûòü èçìåíåíà]
    //         "Ïîëó÷àòåëü": TheOtherCompany
    // Ðåèìïîðò
    //   Òîãäà "Ñòðàíà îòïðàâëåíèÿ": îïðåäåëÿåòñÿ ïî ïîëþ TheOtherCountryCode
    //         "Ñòðàíà íàçíà÷åíèÿ": RU
    //         "Îòïðàâèòåëü": TheOtherCompany
    //         "Ïîëó÷àòåëü": Company [îïðåäåëåíà â ìîìåíò çàãðóçêè ðååñòðà è íå ìîæåò áûòü èçìåíåíà]
    //
    // Ñèñòåìå TheOtherCompany è TheOtherCountryCode íå íóæíû äëÿ òîãî. Ñîâñåì íå íóæíû. 
    // ...................................................................................................

    [Column("id")]
    public int Id { get; set; }

    [Column("filename")]
    public required string FileName { get; set; }

    [Column("dtime")]
    public DateTime DTime { get; set; } = DateTime.UtcNow;

    [Column("deal_number")]
    public string DealNumber { get; set; } = string.Empty;

    [Column("company_id")]
    public int CompanyId { get; set; }

    [JsonIgnore]
    public Company? Company { get; set; }

    [Column("the_other_company_id")]
    public int? TheOtherCompanyId { get; set; }

    [JsonIgnore]
    public Company? TheOtherCompany { get; set; }

    [Column("invoice_number")]
    public string? InvoiceNumber { get; set; }

    [Column("invoice_date")]
    public DateOnly? InvoiceDate { get; set; }

    [Column("the_other_country_code")]
    public short? TheOtherCountryCode { get; set; } 

    [JsonIgnore]
    public Country? TheOtherCountry { get; set; }

    [Column("transportation_type_id")]
    public int TransportationTypeId { get; set; } = 1;
    // { Id = 1, Code = TransportationTypeCode.Avia, Name = "Àâèà" }

    [JsonIgnore]
    public TransportationType? TransportationType { get; set; }

    [Column("customs_procedure_id")]
    public int CustomsProcedureId { get; set; } = 1;
    // { Id = 1, Code = 10, Name = "Ýêñïîðò" }

    [JsonIgnore]
    public CustomsProcedure? CustomsProcedure { get; set; }

    [JsonIgnore]
    public ICollection<BaseParcel> Orders { get; set; } = new List<BaseParcel>();
}
