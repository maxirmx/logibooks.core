using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("wbr_orders")]
[Index(nameof(Shk), Name = "IX_wbr_orders_shk")]
public class WbrOrder : BaseOrder
{
    [Column("row_number")]
    public int RowNumber { get; set; }

    [Column("order_number")]
    public string? OrderNumber { get; set; }

    [Column("invoice_date", TypeName = "date")]
    public DateOnly? InvoiceDate { get; set; }

    [Column("sticker")]
    public string? Sticker { get; set; }

    [Column("shk")]
    public string? Shk { get; set; }

    [Column("sticker_code")]
    public string? StickerCode { get; set; }

    [Column("ext_id")]
    public string? ExtId { get; set; }

    [Column("site_article")]
    public string? SiteArticle { get; set; }

    [Column("heel_height")]
    public string? HeelHeight { get; set; }

    [Column("size")]
    public string? Size { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("gender")]
    public string? Gender { get; set; }

    [Column("brand")]
    public string? Brand { get; set; }

    [Column("fabric_type")]
    public string? FabricType { get; set; }

    [Column("composition")]
    public string? Composition { get; set; }

    [Column("lining")]
    public string? Lining { get; set; }

    [Column("insole")]
    public string? Insole { get; set; }

    [Column("sole")]
    public string? Sole { get; set; }

    [Column("country")]
    public string? Country { get; set; }

    [Column("factory_address")]
    public string? FactoryAddress { get; set; }

    [Column("unit")]
    public string? Unit { get; set; }

    [Column("weight_kg", TypeName = "numeric(10,2)")]
    public decimal? WeightKg { get; set; }

    [Column("quantity", TypeName = "numeric(10)")]
    public decimal? Quantity { get; set; }

    [Column("unit_price", TypeName = "numeric(10,2)")]
    public decimal? UnitPrice { get; set; }

    [Column("currency")]
    public string? Currency { get; set; }

    [Column("barcode")]
    public string? Barcode { get; set; }

    [Column("declaration")]
    public string? Declaration { get; set; }

    [Column("product_link")]
    public string? ProductLink { get; set; }

    [Column("recipient_name")]
    public string? RecipientName { get; set; }

    [Column("recipient_inn")]
    public string? RecipientInn { get; set; }

    [Column("passport_number")]
    public string? PassportNumber { get; set; }

    [Column("pinfl")]
    public string? Pinfl { get; set; }

    [Column("recipient_address")]
    public string? RecipientAddress { get; set; }

    [Column("contact_phone")]
    public string? ContactPhone { get; set; }

    [Column("box_number")]
    public string? BoxNumber { get; set; }

    [Column("supplier")]
    public string? Supplier { get; set; }

    [Column("supplier_inn")]
    public string? SupplierInn { get; set; }

    [Column("category")]
    public string? Category { get; set; }

    [Column("subcategory")]
    public string? Subcategory { get; set; }

    [Column("personal_data")]
    public string? PersonalData { get; set; }

    [Column("customs_clearance")]
    public string? CustomsClearance { get; set; }

    [Column("duty_payment")]
    public string? DutyPayment { get; set; }

    [Column("other_reason")]
    public string? OtherReason { get; set; }
}