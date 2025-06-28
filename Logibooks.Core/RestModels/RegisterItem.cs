namespace Logibooks.Core.RestModels;

public class RegisterItem
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}
