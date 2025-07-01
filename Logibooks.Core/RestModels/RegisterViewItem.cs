using Logibooks.Core.Models;
namespace Logibooks.Core.RestModels;

public class RegisterViewItem
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public List<Order> Orders { get; set; } = [];
    public int OrdersTotal { get; set; }
    public Dictionary<int, int> OrdersByStatus { get; set; } = new();
}
