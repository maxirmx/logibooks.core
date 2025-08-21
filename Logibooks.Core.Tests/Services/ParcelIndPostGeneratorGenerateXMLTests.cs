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

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.Linq;

using Moq;
using NUnit.Framework;

using Logibooks.Core.Models;
using Logibooks.Core.Services;
using Logibooks.Core.Constants;
using Logibooks.Core.Data;
using Logibooks.Core.Interfaces;

namespace Logibooks.Core.Tests.Services
{
    [TestFixture]
    public class ParcelIndPostGeneratorGenerateXMLTests
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Mock<IIndPostXmlService> _xmlServiceMock;
        private AppDbContext _dbContext;
        private ParcelIndPostGenerator _generator;
        private TransportationType _transportationType;
        private CustomsProcedure _customsProcedure;
        private CustomsProcedure _customsProcedure60;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [SetUp]
        public void SetUp()
        {
            _xmlServiceMock = new Mock<IIndPostXmlService>();
            
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"test_db_{Guid.NewGuid()}")
                .Options;
            _dbContext = new AppDbContext(options);
            
            _generator = new ParcelIndPostGenerator(_dbContext, _xmlServiceMock.Object);

            _transportationType = new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа" };
            _customsProcedure = new CustomsProcedure { Id = 1, Code = 10, Name = "Экспорт" };
            _customsProcedure60 = new CustomsProcedure { Id = 2, Code = 60, Name = "Реимпорт" };
            
            _dbContext.TransportationTypes.Add(_transportationType);
            _dbContext.CustomsProcedures.Add(_customsProcedure);
            _dbContext.CustomsProcedures.Add(_customsProcedure60);
            _dbContext.SaveChanges();

        }

        [TearDown]
        public void TearDown()
        {
            _dbContext?.Dispose();
        }

        [Test]
        [TestCase(10, "Individual", "Иванов", "Иван", "Иванович", "Москва", "ул. Тест, д.1", "RU", "Россия", "1", "2", "ООО Тест", "0")]
        [TestCase(60, "Company", null, null, null, "Москва", "ул. Тест", "RU", "Россия", "2", "1", "Иванов Иван Иванович", "0")]
        public void GenerateXML_AllFields_CorrectlyGenerated(int customsCode, string scenario,
            string? lastName, string? firstName, string? patronymic,
            string city, string street, string countryCode, string countryName,
            string consigneeChoice, string consignorChoice, string sender, string type)
        {
            // Arrange
            var country = new Country { IsoNumeric = 643, IsoAlpha2 = countryCode, NameRuShort = countryName };
            var company = new Company {
                Id = 1,
                ShortName = "ООО Тест",
                Kpp = "123456789",
                Inn = "987654321",
                PostalCode = "101000",
                City = "Москва",
                Street = "ул. Тест",
                CountryIsoNumeric = 643,
                Name = "ООО Тест Полное"
            };
            var register = new Register {
                Id = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                TransportationTypeId = _transportationType.Id,
                CustomsProcedureId = customsCode == 10 ? _customsProcedure.Id : _customsProcedure60.Id,
                TheOtherCountryCode = 643,
                CompanyId = 1,
                FileName = "test.xml"
            };

            _dbContext.Countries.Add(country);
            _dbContext.Companies.Add(company);
            _dbContext.Registers.Add(register);
            _dbContext.SaveChanges();

            var loadedRegister = _dbContext.Registers
                .Include(r => r.TheOtherCountry)
                .Include(r => r.TransportationType)
                .Include(r => r.CustomsProcedure)
                .Include(r => r.Company)
                    .ThenInclude(c => c!.Country)
                .First(r => r.Id == 1);

            BaseParcel order;
            if (scenario == "Individual")
            {
                order = new OzonParcel {
                    Id = 1,
                    Register = loadedRegister,
                    RegisterId = 1,
                    PostingNumber = "POST123",
                    LastName = lastName,
                    FirstName = firstName,
                    Patronymic = patronymic,
                    City = city,
                    Address = street,
                    CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
                };
            }
            else
            {
                order = new OzonParcel {
                    Id = 1,
                    Register = loadedRegister,
                    RegisterId = 1,
                    LastName = "Иванов",
                    FirstName = "Иван",
                    Patronymic = "Иванович",
                    City = city,
                    Address = street,
                    PassportSeries = "AB123456",
                    PassportNumber = "AB123456",
                    CheckStatusId = (int)ParcelCheckStatusCode.Approved
                };
            }

            _dbContext.OzonOrders.Add((OzonParcel)order);
            _dbContext.SaveChanges();

            _xmlServiceMock.Setup(x => x.CreateXml(It.IsAny<IDictionary<string, string?>>(), It.IsAny<IEnumerable<IDictionary<string, string?>>>()))
                .Returns("<xml></xml>");

            // Act
            var xml = _generator.GenerateXML(order);

            // Assert
            _xmlServiceMock.Verify(x => x.CreateXml(It.Is<IDictionary<string, string?>>(fields =>
                fields["CONSIGNEE_CHOICE"] == consigneeChoice &&
                fields["CONSIGNOR_CHOICE"] == consignorChoice &&
                fields["SENDER"] == sender &&
                fields["CITY"] == city &&
                fields["STREETHOUSE"] == street &&
                fields["CONSIGNEE_ADDRESS_COUNTRYCODE"] == countryCode &&
                fields["CONSIGNEE_ADDRESS_COUNRYNAME"] == countryName &&
                fields["TYPE"] == type
            ), It.IsAny<IEnumerable<IDictionary<string, string?>>>()), Times.Once);
        }

        [Test]
        public void GenerateXML_WbrOrder_GoodsItemsGenerated()
        {
            // Arrange
            var country = new Country { IsoNumeric = 643, IsoAlpha2 = "RU", NameRuShort = "Россия" };
            var company = new Company { 
                Id = 1, 
                ShortName = "ООО Тест", 
                Kpp = "123456789", 
                Inn = "987654321", 
                PostalCode = "101000", 
                City = "Москва", 
                Street = "ул. Тест",
                CountryIsoNumeric = 643,
                Name = "ООО Тест Полное"
            };
            var register = new Register {
                Id = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                TransportationTypeId = _transportationType.Id,
                CustomsProcedureId = _customsProcedure.Id,
                TheOtherCountryCode = 643,
                CompanyId = 1,
                FileName = "test.xml"
            };

            _dbContext.Countries.Add(country);
            _dbContext.Companies.Add(company);
            _dbContext.Registers.Add(register);
            _dbContext.SaveChanges();

            // Load the register with all related data
            var loadedRegister = _dbContext.Registers
                .Include(r => r.TheOtherCountry)
                .Include(r => r.TransportationType)
                .Include(r => r.CustomsProcedure)
                .Include(r => r.Company)
                    .ThenInclude(c => c!.Country)
                .First(r => r.Id == 1);

            var order = new WbrOrder { 
                Id = 1,
                Register = loadedRegister,
                RegisterId = 1,
                Shk = "SHK123", 
                RecipientName = "Иванов Иван Иванович", 
                RecipientAddress = "Россия, Москва, ул. Тест, д.1", 
                ProductName = "Товар", 
                Quantity = 2, 
                UnitPrice = 10.5m, 
                WeightKg = 1.234m, 
                ProductLink = "https://example.com/product",
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            };

            _dbContext.WbrOrders.Add(order);
            _dbContext.SaveChanges();

            _xmlServiceMock.Setup(x => x.CreateXml(It.IsAny<IDictionary<string, string?>>(), It.IsAny<IEnumerable<IDictionary<string, string?>>>()))
                .Returns("<xml></xml>");

            // Act
            var xml = _generator.GenerateXML(order);

            // Assert
            _xmlServiceMock.Verify(x => x.CreateXml(It.IsAny<IDictionary<string, string?>>(), It.Is<IEnumerable<IDictionary<string, string?>>>(goodsItems =>
                goodsItems != null &&
                goodsItems.Any(item => 
                    item["DESCR"] != null && item["DESCR"]!.Contains("УИН:") && item["DESCR"]!.Contains("Товар") &&
                    item["QTY"] == "2" && 
                    item["COST"] != null &&
                    item["WEIGHT"] != null &&
                    item["URL"] == "https://example.com/product")
            )), Times.Once);
        }

        [Test]
        public void GenerateXML_NullFields_DefaultsUsed()
        {
            // Arrange
            var register = new Register { 
                Id = 1,
                InvoiceDate = null,
                TransportationTypeId = 999,
                CustomsProcedureId = 999,
                CompanyId = 1, // Non-nullable, set to default
                TheOtherCountryCode = null, // This can be null
                FileName = "test.xml" 
            };

            _dbContext.Registers.Add(register);
            _dbContext.SaveChanges();

            // Load the register (which will have null related entities because we didn't add them to the context)
            var loadedRegister = _dbContext.Registers.First(r => r.Id == 1);

            var order = new OzonParcel { 
                Id = 1,
                Register = loadedRegister,
                RegisterId = 1,
                CheckStatusId = (int)ParcelCheckStatusCode.NoIssues
            };

            _dbContext.OzonOrders.Add(order);
            _dbContext.SaveChanges();

            _xmlServiceMock.Setup(x => x.CreateXml(It.IsAny<IDictionary<string, string?>>(), It.IsAny<IEnumerable<IDictionary<string, string?>>>()))
                .Returns("<xml></xml>");

            // Act
            var xml = _generator.GenerateXML(order);

            // Assert
            _xmlServiceMock.Verify(x => x.CreateXml(It.Is<IDictionary<string, string?>>(fields =>
                fields["AVIANUM"] == Placeholders.NotSet &&
                fields["AVIADATE"] == Placeholders.NotSet &&
                fields["TYPE"] == Placeholders.NotSet &&
                fields["ORGCOUNTRY"] == Placeholders.NotSet
            ), It.IsAny<IEnumerable<IDictionary<string, string?>>>()), Times.Once);
        }
    }
}
