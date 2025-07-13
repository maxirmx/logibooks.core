namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class OrderStatusDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public OrderStatusDto() {}
    public OrderStatusDto(OrderStatus status)
    {
        Id = status.Id;
        Title = status.Title;
    }

    public OrderStatus ToModel()
    {
        return new OrderStatus
        {
            Id = Id,
            Title = Title
        };
    }
}
