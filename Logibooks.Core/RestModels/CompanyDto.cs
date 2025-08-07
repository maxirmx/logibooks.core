// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

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
