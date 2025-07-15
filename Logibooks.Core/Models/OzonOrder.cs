using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("ozon_orders")]
public class OzonOrder : BaseOrder
{
    [Column("posting_number")]
    public string? PostingNumber { get; set; }

    [Column("ozon_id")]
    public string? OzonId { get; set; }
}
