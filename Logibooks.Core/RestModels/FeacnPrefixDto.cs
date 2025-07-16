using Logibooks.Core.Models;

namespace Logibooks.Core.RestModels;

public class FeacnPrefixDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Comment { get; set; }
    public int FeacnOrderId { get; set; }

    public FeacnPrefixDto() { }
    public FeacnPrefixDto(FEACNPrefix p)
    {
        Id = p.Id;
        Code = p.Code;
        Description = p.Description;
        Comment = p.Comment;
        FeacnOrderId = p.FeacnOrderId;
    }
}
