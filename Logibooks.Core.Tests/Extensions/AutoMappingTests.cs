using Microsoft.Extensions.DependencyInjection;

using AutoMapper;
using NUnit.Framework;
using Logibooks.Core.Models;
using Logibooks.Core.RestModels;
using Logibooks.Core.Extensions;

namespace Logibooks.Core.Tests.Extensions;

[TestFixture]
public class AutoMapperIntegrationTests
{
#pragma warning disable CS8618
    private ServiceProvider _serviceProvider;
    private IMapper _mapper;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddProfile<OrderMappingProfile>());
        _serviceProvider = services.BuildServiceProvider();
        _mapper = _serviceProvider.GetRequiredService<IMapper>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public void DependencyInjection_AutoMapper_IsConfiguredCorrectly()
    {
        // Arrange & Act  
        var mapper = _serviceProvider.GetRequiredService<IMapper>();

        // Assert  
        Assert.That(mapper, Is.Not.Null);
        Assert.That(mapper.ConfigurationProvider, Is.Not.Null);
    }

    [Test]
    public void DependencyInjection_Mapping_WorksAsExpected()
    {
        // Arrange  
        var updateItem = new OrderUpdateItem
        {
            StatusId = 5,
            OrderNumber = "DI_TEST_123"
        };

        var order = new Order
        {
            Id = 1,
            StatusId = 1,
            OrderNumber = "OLD"
        };

        // Act  
        _mapper.Map(updateItem, order);

        // Assert  
        Assert.That(order.StatusId, Is.EqualTo(5));
        Assert.That(order.OrderNumber, Is.EqualTo("DI_TEST_123"));
        Assert.That(order.Id, Is.EqualTo(1)); // Should remain unchanged
    }

    [Test]
    public void AutoMapper_MapsWeightKgDecimal()
    {
        var updateItem = new OrderUpdateItem { WeightKg = 1.234m };
        var order = new Order();

        _mapper.Map(updateItem, order);

        Assert.That(order.WeightKg, Is.EqualTo(1.234m));
    }
}

