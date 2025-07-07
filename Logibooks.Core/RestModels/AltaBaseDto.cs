namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public abstract class AltaBaseDto<TModel> where TModel : AltaBase, new()
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Number { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Comment { get; set; }

    protected AltaBaseDto() {}

    protected AltaBaseDto(TModel item)
    {
        Id = item.Id;
        Url = item.Url;
        Number = item.Number;
        Code = item.Code;
        Name = item.Name;
        Comment = item.Comment;
    }

    public virtual TModel ToModel()
    {
        return new TModel
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
