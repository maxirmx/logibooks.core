using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("feacn_prefix_exceptions")]
public class FEACNPrefixException
{
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public required string Code { get; set; } = string.Empty;

    [Column("feacn_prefix_id")]
    public int FeacnPrefixId { get; set; }
    public FEACNPrefix FeacnPrefix { get; set; } = null!;
}
