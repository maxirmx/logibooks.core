using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("customs_procedures")]
public class CustomsProcedure
{
    [Column("id")]
    public int Id { get; set; }

    [Column("code")]
    public short Code { get; set; }

    [Column("name")]
    public required string Name { get; set; }
}
