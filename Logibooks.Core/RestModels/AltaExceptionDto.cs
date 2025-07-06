namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class AltaExceptionDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Comment { get; set; }

    public AltaExceptionDto() {}
    public AltaExceptionDto(AltaException item)
    {
        Id = item.Id;
        Url = item.Url;
        Number = item.Number;
        Code = item.Code;
        Name = item.Name;
        Comment = item.Comment;
    }

    public AltaException ToModel()
    {
        return new AltaException
        {
            Id = Id,
            Url = Url,
            Number = Number,
            Code = Code,
            Name = Name,
            Comment = Comment
        };
    }
}
