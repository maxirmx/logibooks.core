using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Logibooks.Core.Models;

[Table("base_orders")]
[Index(nameof(TnVed), Name = "IX_base_orders_tn_ved")]
public abstract class BaseOrder
{
    [Column("id")]
    public int Id { get; set; }

    [Column("register_id")]
    public int RegisterId { get; set; }

    [JsonIgnore]
    public Register Register { get; set; } = null!;

    [Column("status_id")]
    public int StatusId { get; set; }
    public OrderStatus Status { get; set; } = null!;

    [Column("check_status_id")]
    public int CheckStatusId { get; set; }
    public OrderCheckStatus CheckStatus { get; set; } = null!;

    [Column("product_name")]
    public string? ProductName { get; set; }

    [Column("tn_ved")]
    public string? TnVed { get; set; }

    [Column("country_code")]
    public short CountryCode { get; set; }

    public virtual Country Country { get; set; } = null!;

    public virtual ICollection<BaseOrderStopWord> BaseOrderStopWords { get; set; } = new List<BaseOrderStopWord>();

    public virtual ICollection<BaseOrderFeacnPrefix> BaseOrderFeacnPrefixes { get; set; } = new List<BaseOrderFeacnPrefix>();
}
