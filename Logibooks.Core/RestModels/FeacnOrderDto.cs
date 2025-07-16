using Logibooks.Core.Models;

namespace Logibooks.Core.RestModels;

public class FeacnOrderDto
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string? Url { get; set; }
    public string? Comment { get; set; }

    public FeacnOrderDto() { }
    public FeacnOrderDto(FEACNOrder o)
    {
        Id = o.Id;
        Number = o.Number;
        Url = o.Url;
        Comment = o.Comment;
    }
}
