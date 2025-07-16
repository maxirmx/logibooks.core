using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("feacn_orders")]
public class FEACNOrder
{
    [Column("id")]
    public int Id { get; set; }

    [Column("number")]
    public int Number { get; set; }

    [Column("url")]
    public string? Url { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    public ICollection<FEACNPrefix> FeacnPrefixes { get; set; } = new List<FEACNPrefix>();
}
