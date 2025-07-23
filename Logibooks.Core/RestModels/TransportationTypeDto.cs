namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class TransportationTypeDto
{
    public int Id { get; set; }
    public TransportationTypeCode Code { get; set; }
    public string Name { get; set; } = string.Empty;

    public TransportationTypeDto() {}
    public TransportationTypeDto(TransportationType type)
    {
        Id = type.Id;
        Code = type.Code;
        Name = type.Name;
    }

    public TransportationType ToModel()
    {
        return new TransportationType
        {
            Id = Id,
            Code = Code,
            Name = Name
        };
    }
}
