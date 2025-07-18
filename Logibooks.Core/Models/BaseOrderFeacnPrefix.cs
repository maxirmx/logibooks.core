using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("base_order_feacn_prefixes")]
public class BaseOrderFeacnPrefix
{
    [Column("base_order_id")]
    public int BaseOrderId { get; set; }
    public BaseOrder BaseOrder { get; set; } = null!;

    [Column("feacn_prefix_id")]
    public int FeacnPrefixId { get; set; }
    public FeacnPrefix FeacnPrefix { get; set; } = null!;
}
