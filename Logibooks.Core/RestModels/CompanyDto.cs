// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application

namespace Logibooks.Core.RestModels;

using Logibooks.Core.Models;

public class CompanyDto
{
    public int Id { get; set; }
    public string Inn { get; set; } = string.Empty;
    public string Kpp { get; set; } = string.Empty;
    public string Ogrn { get; set; } = string.Empty;
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
        Ogrn = c.Ogrn;
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
            Ogrn = Ogrn,
            Name = Name,
            ShortName = ShortName,
            CountryIsoNumeric = CountryIsoNumeric,
            PostalCode = PostalCode,
            City = City,
            Street = Street
        };
    }

    public void UpdateModel(Company company)
    {
        company.Inn = Inn;
        company.Kpp = Kpp;
        company.Ogrn = Ogrn;
        company.Name = Name;
        company.ShortName = ShortName;
        company.CountryIsoNumeric = CountryIsoNumeric;
        company.PostalCode = PostalCode;
        company.City = City;
        company.Street = Street;
    }
}
