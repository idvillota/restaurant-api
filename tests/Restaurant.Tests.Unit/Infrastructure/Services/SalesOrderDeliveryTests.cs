using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Moq;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Crm.Customers;
using Restaurant.Application.Mapping;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class SalesOrderDeliveryTests
{
    [Fact]
    public async Task StartDeliveryOrder_creates_customer_slot_and_snapshots_address()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);

        var order = await sut.StartDeliveryOrderAsync(new StartDeliveryCustomerDto
        {
            TaxId = "1.098.765",
            Name = "Ana Pérez",
            Phone = "3001234567",
            Address = "Cra 7 #12-34",
            Email = "ana@test.local",
        });

        Assert.True(order.IsDelivery);
        Assert.Equal("Ana Pérez", order.DeliveryCustomerName);
        Assert.Equal("Cra 7 #12-34", order.DeliveryAddress);
        Assert.Equal("3001234567", order.DeliveryPhone);
        Assert.NotNull(order.DiningTableId);

        var customer = await fx.Db.Customers.SingleAsync(c => c.TaxId == "1098765");
        Assert.Equal("Ana Pérez", customer.Name);
        Assert.Equal("Cra 7 #12-34", customer.Address);

        var slot = await fx.Db.DiningTables.SingleAsync(t => t.Id == order.DiningTableId);
        Assert.True(slot.IsDeliverySlot);
        Assert.Equal(ETableStatus.Busy, slot.Status);
    }

    [Fact]
    public async Task StartDeliveryOrder_reuses_customer_on_second_call()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);
        var dto = new StartDeliveryCustomerDto
        {
            TaxId = "555",
            Name = "Luis",
            Phone = "301",
            Address = "Calle 1",
        };
        await sut.StartDeliveryOrderAsync(dto);
        dto.Address = "Calle 2";
        dto.Phone = "302";
        var second = await sut.StartDeliveryOrderAsync(dto);

        Assert.Equal(1, await fx.Db.Customers.CountAsync());
        Assert.Equal("Calle 2", second.DeliveryAddress);
        Assert.Equal("Calle 2", (await fx.Db.Customers.SingleAsync()).Address);
        Assert.Equal(2, await fx.Db.SalesOrders.CountAsync());
    }

    [Fact]
    public async Task StartDeliveryOrder_temporary_address_does_not_overwrite_profile()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);

        await sut.StartDeliveryOrderAsync(new StartDeliveryCustomerDto
        {
            TaxId = "555",
            Name = "Luis",
            Phone = "301",
            Address = "Calle 1",
        });

        var second = await sut.StartDeliveryOrderAsync(new StartDeliveryCustomerDto
        {
            TaxId = "555",
            Name = "Luis",
            Phone = "301",
            Address = "Casa de mamá",
            UpdateProfileAddress = false,
        });

        Assert.Equal("Casa de mamá", second.DeliveryAddress);
        Assert.Equal("Calle 1", (await fx.Db.Customers.SingleAsync()).Address);
    }

    private static SalesOrderService CreateSut(TenantDbFixture fx)
    {
        var day = new Mock<IOperationalBusinessDayService>();
        day.Setup(d => d.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectiveOperationalDay(
                DateOnly.FromDateTime(DateTime.UtcNow),
                DateOnly.FromDateTime(DateTime.UtcNow),
                false,
                5,
                DailyClosureStatus.Open));

        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SalesOrderMappingProfile>();
            cfg.AddProfile<DiningTableMappingProfile>();
            cfg.AddProfile<CustomerMappingProfile>();
        }).CreateMapper();

        var customers = new CustomerService(
            fx.Repository<Customer>(),
            fx.Repository<Reservation>(),
            fx.UnitOfWork,
            mapper);

        return new SalesOrderService(
            fx.Repository<SalesOrder>(),
            fx.Repository<SalesOrderLine>(),
            fx.Repository<SalesOrderLineExcludedIngredient>(),
            fx.Repository<DiningTable>(),
            fx.Repository<Product>(),
            fx.Repository<ProductIngredient>(),
            fx.UnitOfWork,
            mapper,
            Mock.Of<IInventoryAvailabilityService>(),
            Mock.Of<IKitchenTicketService>(),
            Mock.Of<IKitchenPrinterService>(),
            day.Object,
            fx.TenantContext,
            fx.Db,
            customers);
    }
}
