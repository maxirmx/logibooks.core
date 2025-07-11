namespace Logibooks.Core.RestModels;

public class RegisterViewItem
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int OrdersTotal { get; set; }
    public Dictionary<int, int> OrdersByStatus { get; set; } = new();
}
