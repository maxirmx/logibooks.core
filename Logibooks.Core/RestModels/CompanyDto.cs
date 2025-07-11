namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class CompanyDto
{
    public int Id { get; set; }
    public string Inn { get; set; } = string.Empty;
    public string Kpp { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public short CountryIsoNumeric { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;

    public CompanyDto() {}
    public CompanyDto(Company c)
    {
        Id = c.Id;
        Inn = c.Inn;
        Kpp = c.Kpp;
        Name = c.Name;
        ShortName = c.ShortName;
        CountryIsoNumeric = c.CountryIsoNumeric;
        PostalCode = c.PostalCode;
        City = c.City;
        Street = c.Street;
    }

    public Company ToModel()
    {
        return new Company
        {
            Id = Id,
            Inn = Inn,
            Kpp = Kpp,
            Name = Name,
            ShortName = ShortName,
            CountryIsoNumeric = CountryIsoNumeric,
            PostalCode = PostalCode,
            City = City,
            Street = Street
        };
    }
}
