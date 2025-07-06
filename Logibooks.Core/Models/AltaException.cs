using System.ComponentModel.DataAnnotations.Schema;

namespace Logibooks.Core.Models;

[Table("alta_exceptions")]
public class AltaException
{
    [Column("id")]
    public int Id { get; set; }

    [Column("url")]
    public string Url { get; set; } = string.Empty;

    [Column("number")]
    public string? Number { get; set; }

    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("comment")]
    public string? Comment { get; set; }
}
