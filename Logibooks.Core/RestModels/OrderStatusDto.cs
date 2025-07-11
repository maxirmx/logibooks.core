namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class OrderStatusDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public OrderStatusDto() {}
    public OrderStatusDto(OrderStatus status)
    {
        Id = status.Id;
        Name = status.Name;
        Title = status.Title;
    }

    public OrderStatus ToModel()
    {
        return new OrderStatus
        {
            Id = Id,
            Name = Name,
            Title = Title
        };
    }
}
