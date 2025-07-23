using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("transportation_types")]
public class TransportationType
{
    [Column("id")]
    public int Id { get; set; }

    [Column("code", TypeName = "numeric(1)")]
    public TransportationTypeCode Code { get; set; }

    [Column("name")]
    public required string Name { get; set; }
}
