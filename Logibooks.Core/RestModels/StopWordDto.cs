namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class StopWordDto
{
    public int Id { get; set; }
    public string Word { get; set; } = string.Empty;
    public bool ExactMatch { get; set; }

    public StopWordDto() {}
    public StopWordDto(StopWord sw)
    {
        Id = sw.Id;
        Word = sw.Word;
        ExactMatch = sw.ExactMatch;
    }

    public StopWord ToModel()
    {
        return new StopWord
        {
            Id = Id,
            Word = Word,
            ExactMatch = ExactMatch
        };
    }
}
