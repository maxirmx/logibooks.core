namespace Logibooks.Core.RestModels;

public class ValidationProgress
{
    public Guid HandleId { get; set; }
    public int Total { get; set; }
    public int Processed { get; set; }
    public bool Finished { get; set; }
    public string? Error { get; set; }
}
