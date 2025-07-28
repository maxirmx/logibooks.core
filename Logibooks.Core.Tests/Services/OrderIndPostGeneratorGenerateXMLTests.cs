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

namespace Logibooks.Core.Tests.Services
{
    [TestFixture]
    public class OrderIndPostGeneratorGenerateXMLTests
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Mock<IIndPostXmlService> _xmlServiceMock;
        private AppDbContext _dbContext;
        private OrderIndPostGenerator _generator;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [SetUp]
        public void SetUp()
        {
            _xmlServiceMock = new Mock<IIndPostXmlService>();
            
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"test_db_{Guid.NewGuid()}")
                .Options;
            _dbContext = new AppDbContext(options);
            
            _generator = new OrderIndPostGenerator(_dbContext, _xmlServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext?.Dispose();
        }

        [Test]
        public void GenerateXML_CustomsProcedure10_IndividualFieldsGenerated()
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
            var transportationType = new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа" };
            var customsProcedure = new CustomsProcedure { Id = 1, Code = 10, Name = "Экспорт" };
            var register = new Register {
                Id = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                TransportationTypeId = 1,
                CustomsProcedureId = 1,
                DestCountryCode = 643,
                CompanyId = 1,
                FileName = "test.xml"
            };

            _dbContext.Countries.Add(country);
            _dbContext.Companies.Add(company);
            _dbContext.TransportationTypes.Add(transportationType);
            _dbContext.CustomsProcedures.Add(customsProcedure);
            _dbContext.Registers.Add(register);
            _dbContext.SaveChanges();

            // Load the register with all related data
            var loadedRegister = _dbContext.Registers
                .Include(r => r.DestinationCountry)
                .Include(r => r.TransportationType)
                .Include(r => r.CustomsProcedure)
                .Include(r => r.Company)
                    .ThenInclude(c => c!.Country)
                .First(r => r.Id == 1);

            var order = new OzonOrder { 
                Id = 1,
                Register = loadedRegister, 
                RegisterId = 1,
                PostingNumber = "POST123", 
                LastName = "Иванов", 
                FirstName = "Иван", 
                Patronymic = "Иванович", 
                City = "Москва", 
                Address = "ул. Тест, д.1" 
            };

            _dbContext.OzonOrders.Add(order);
            _dbContext.SaveChanges();

            _xmlServiceMock.Setup(x => x.CreateXml(It.IsAny<IDictionary<string, string?>>(), It.IsAny<IEnumerable<IDictionary<string, string?>>>()))
                .Returns("<xml></xml>");

            // Act
            var xml = _generator.GenerateXML(order);

            // Assert
            _xmlServiceMock.Verify(x => x.CreateXml(It.Is<IDictionary<string, string?>>(fields =>
                fields["CONSIGNEE_CHOICE"] == "1" &&
                fields["PERSONSURNAME"] == "Иванов" &&
                fields["PERSONNAME"] == "Иван" &&
                fields["PERSONMIDDLENAME"] == "Иванович" &&
                fields["CITY"] == "Москва" &&
                fields["STREETHOUSE"] == "ул. Тест, д.1" &&
                fields["CONSIGNEE_ADDRESS_COUNTRYCODE"] == "RU" &&
                fields["CONSIGNEE_ADDRESS_COUNRYNAME"] == "Россия" &&
                fields["CONSIGNOR_CHOICE"] == "2" &&
                fields["SENDER"] == "ООО Тест"
            ), It.IsAny<IEnumerable<IDictionary<string, string?>>>()), Times.Once);
        }

        [Test]
        public void GenerateXML_CustomsProcedure60_CompanyFieldsGenerated()
        {
            // Arrange
            var country = new Country { IsoNumeric = 643, IsoAlpha2 = "RU", NameRuShort = "Россия" };
            var company = new Company { 
                Id = 1, 
                ShortName = "ООО Тест", 
                Kpp = "123456789", 
                Inn = "987654321", 
                City = "Москва", 
                Street = "ул. Тест",
                CountryIsoNumeric = 643,
                Name = "ООО Тест Полное",
                PostalCode = "101000"
            };
            var transportationType = new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа" };
            var customsProcedure = new CustomsProcedure { Id = 1, Code = 60, Name = "Реимпорт" };
            var register = new Register {
                Id = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                TransportationTypeId = 1,
                CustomsProcedureId = 1,
                DestCountryCode = 643,
                CompanyId = 1,
                FileName = "test.xml"
            };

            _dbContext.Countries.Add(country);
            _dbContext.Companies.Add(company);
            _dbContext.TransportationTypes.Add(transportationType);
            _dbContext.CustomsProcedures.Add(customsProcedure);
            _dbContext.Registers.Add(register);
            _dbContext.SaveChanges();

            // Load the register with all related data
            var loadedRegister = _dbContext.Registers
                .Include(r => r.DestinationCountry)
                .Include(r => r.TransportationType)
                .Include(r => r.CustomsProcedure)
                .Include(r => r.Company)
                    .ThenInclude(c => c!.Country)
                .First(r => r.Id == 1);

            var order = new OzonOrder { 
                Id = 1,
                Register = loadedRegister,
                RegisterId = 1,
                LastName = "Иванов", 
                FirstName = "Иван", 
                Patronymic = "Иванович", 
                City = "Москва", 
                Address = "ул. Тест, д.1", 
                PassportSeries = "AB", 
                PassportNumber = "123456" 
            };

            _dbContext.OzonOrders.Add(order);
            _dbContext.SaveChanges();

            _xmlServiceMock.Setup(x => x.CreateXml(It.IsAny<IDictionary<string, string?>>(), It.IsAny<IEnumerable<IDictionary<string, string?>>>()))
                .Returns("<xml></xml>");

            // Act
            var xml = _generator.GenerateXML(order);

            // Assert
            _xmlServiceMock.Verify(x => x.CreateXml(It.Is<IDictionary<string, string?>>(fields =>
                fields["CONSIGNEE_CHOICE"] == "2" &&
                fields["CONSIGNEE_SHORTNAME"] == "ООО Тест" &&
                fields["CONSIGNOR_CHOICE"] == "1" &&
                fields["SENDER"] == "Иванов Иван Иванович" &&
                fields["CONSIGNOR_IDENTITYCARD_IDENTITYCARDCODE"] == "10" &&
                fields["CONSIGNOR_IDENTITYCARD_IDENTITYCARDSERIES"] == "AB" &&
                fields["CONSIGNOR_IDENTITYCARD_IDENTITYCARDNUMBER"] == "123456"
            ), It.IsAny<IEnumerable<IDictionary<string, string?>>>()), Times.Once);
        }

        [Test]
        public void GenerateXML_OtherCustomsProcedure_DefaultFieldsGenerated()
        {
            // Arrange
            var country = new Country { IsoNumeric = 643, IsoAlpha2 = "RU", NameRuShort = "Россия" };
            var company = new Company { 
                Id = 1, 
                ShortName = "ООО Тест", 
                Kpp = "123456789", 
                Inn = "987654321", 
                City = "Москва", 
                Street = "ул. Тест",
                CountryIsoNumeric = 643,
                Name = "ООО Тест Полное",
                PostalCode = "101000"
            };
            var transportationType = new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа" };
            var customsProcedure = new CustomsProcedure { Id = 1, Code = 99, Name = "Другое" };
            var register = new Register {
                Id = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                TransportationTypeId = 1,
                CustomsProcedureId = 1,
                DestCountryCode = 643,
                CompanyId = 1,
                FileName = "test.xml"
            };

            _dbContext.Countries.Add(country);
            _dbContext.Companies.Add(company);
            _dbContext.TransportationTypes.Add(transportationType);
            _dbContext.CustomsProcedures.Add(customsProcedure);
            _dbContext.Registers.Add(register);
            _dbContext.SaveChanges();

            // Load the register with all related data
            var loadedRegister = _dbContext.Registers
                .Include(r => r.DestinationCountry)
                .Include(r => r.TransportationType)
                .Include(r => r.CustomsProcedure)
                .Include(r => r.Company)
                    .ThenInclude(c => c!.Country)
                .First(r => r.Id == 1);

            var order = new OzonOrder { 
                Id = 1,
                Register = loadedRegister,
                RegisterId = 1,
                LastName = "Иванов", 
                FirstName = "Иван", 
                Patronymic = "Иванович", 
                City = "Москва", 
                Address = "ул. Тест, д.1" 
            };

            _dbContext.OzonOrders.Add(order);
            _dbContext.SaveChanges();

            _xmlServiceMock.Setup(x => x.CreateXml(It.IsAny<IDictionary<string, string?>>(), It.IsAny<IEnumerable<IDictionary<string, string?>>>()))
                .Returns("<xml></xml>");

            // Act
            var xml = _generator.GenerateXML(order);

            // Assert
            _xmlServiceMock.Verify(x => x.CreateXml(It.Is<IDictionary<string, string?>>(fields =>
                !fields.ContainsKey("CONSIGNEE_CHOICE") &&
                !fields.ContainsKey("CONSIGNOR_CHOICE")
            ), It.IsAny<IEnumerable<IDictionary<string, string?>>>()), Times.Once);
        }

        [Test]
        public void GenerateXML_OzonOrder_GoodsItemsGenerated()
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
            var transportationType = new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа" };
            var customsProcedure = new CustomsProcedure { Id = 1, Code = 10, Name = "Экспорт" };
            var register = new Register {
                Id = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                TransportationTypeId = 1,
                CustomsProcedureId = 1,
                DestCountryCode = 643,
                CompanyId = 1,
                FileName = "test.xml"
            };

            _dbContext.Countries.Add(country);
            _dbContext.Companies.Add(company);
            _dbContext.TransportationTypes.Add(transportationType);
            _dbContext.CustomsProcedures.Add(customsProcedure);
            _dbContext.Registers.Add(register);
            _dbContext.SaveChanges();

            // Load the register with all related data
            var loadedRegister = _dbContext.Registers
                .Include(r => r.DestinationCountry)
                .Include(r => r.TransportationType)
                .Include(r => r.CustomsProcedure)
                .Include(r => r.Company)
                    .ThenInclude(c => c!.Country)
                .First(r => r.Id == 1);

            var order = new OzonOrder { 
                Id = 1,
                Register = loadedRegister,
                RegisterId = 1,
                PostingNumber = "POST123", 
                LastName = "Иванов", 
                FirstName = "Иван", 
                Patronymic = "Иванович", 
                City = "Москва", 
                Address = "ул. Тест, д.1", 
                ProductName = "Товар", 
                Article = "ART123", 
                Quantity = 2, 
                UnitPrice = 10.5m, 
                WeightKg = 1.234m, 
                ProductLink = "https://ozon.ru/product/ozon-product" 
            };

            _dbContext.OzonOrders.Add(order);
            _dbContext.SaveChanges();

            _xmlServiceMock.Setup(x => x.CreateXml(It.IsAny<IDictionary<string, string?>>(), It.IsAny<IEnumerable<IDictionary<string, string?>>>()))
                .Returns("<xml></xml>");

            // Act
            var xml = _generator.GenerateXML(order);

            // Assert
            _xmlServiceMock.Verify(x => x.CreateXml(It.IsAny<IDictionary<string, string?>>(), It.Is<IEnumerable<IDictionary<string, string?>>>(goodsItems =>
                goodsItems != null &&
                goodsItems.Any(item => 
                    item["DESCR"] != null && item["DESCR"]!.Contains("УИН:") &&
                    item["QTY"] == "2" && 
                    item["COST"] != null &&
                    item["WEIGHT"] != null &&
                    item["URL"] == "https://ozon.ru/product/ozon-product")
            )), Times.Once);
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
            var transportationType = new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа" };
            var customsProcedure = new CustomsProcedure { Id = 1, Code = 10, Name = "Экспорт" };
            var register = new Register {
                Id = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                TransportationTypeId = 1,
                CustomsProcedureId = 1,
                DestCountryCode = 643,
                CompanyId = 1,
                FileName = "test.xml"
            };

            _dbContext.Countries.Add(country);
            _dbContext.Companies.Add(company);
            _dbContext.TransportationTypes.Add(transportationType);
            _dbContext.CustomsProcedures.Add(customsProcedure);
            _dbContext.Registers.Add(register);
            _dbContext.SaveChanges();

            // Load the register with all related data
            var loadedRegister = _dbContext.Registers
                .Include(r => r.DestinationCountry)
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
                ProductLink = "https://example.com/product" 
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
                TransportationTypeId = 1, // Non-nullable, set to default
                CustomsProcedureId = 1, // Non-nullable, set to default
                CompanyId = 1, // Non-nullable, set to default
                DestCountryCode = null, // This can be null
                FileName = "test.xml" 
            };

            _dbContext.Registers.Add(register);
            _dbContext.SaveChanges();

            // Load the register (which will have null related entities because we didn't add them to the context)
            var loadedRegister = _dbContext.Registers.First(r => r.Id == 1);

            var order = new OzonOrder { 
                Id = 1,
                Register = loadedRegister,
                RegisterId = 1
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
