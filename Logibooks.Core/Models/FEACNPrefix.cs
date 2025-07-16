using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("feacn_prefixes")]
public class FEACNPrefix
{
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public required string Code { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("feacn_order_id")]
    public int FeacnOrderId { get; set; }
    public FEACNOrder FeacnOrder { get; set; } = null!;

    public ICollection<FEACNPrefixException> FeacnPrefixExceptions { get; set; } = new List<FEACNPrefixException>();
    public ICollection<BaseOrderFeacnPrefix> BaseOrderFeacnPrefixes { get; set; } = new List<BaseOrderFeacnPrefix>();
}
