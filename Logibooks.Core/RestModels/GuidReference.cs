namespace Logibooks.Core.RestModels;

public class GuidReference
{
    public Guid Id { get; set; }
    public override string ToString() => $"Reference: {Id}";
}
