using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Moq;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.SalesOrders;
using Restaurant.Application.Mapping;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class SalesOrderCounterTests
{
    [Fact]
    public async Task StartCounterOrder_is_rejected_when_the_tenant_is_not_a_food_plaza()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.StartCounterOrderAsync(new StartCounterOrderDto
            {
                CustomerName = "Ana",
                Phone = "3001234567",
            }));

        Assert.Contains("no tiene servicio de mostrador", ex.Message);
        Assert.Equal(0, await fx.Db.SalesOrders.CountAsync());
    }

    [Fact]
    public async Task StartCounterOrder_snapshots_name_and_phone_without_a_table()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var sut = CreateSut(fx);

        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "  Ana Pérez ",
            Phone = "300 123 4567",
        });

        Assert.True(order.IsCounterPickup);
        Assert.False(order.IsDelivery);
        Assert.Null(order.DiningTableId);
        Assert.Equal("Ana Pérez", order.CounterCustomerName);
        Assert.Equal("3001234567", order.CounterPhone);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);
        Assert.Equal(0, await fx.Db.DiningTables.CountAsync());
    }

    [Fact]
    public async Task StartCounterOrder_requires_a_usable_phone()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var sut = CreateSut(fx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.StartCounterOrderAsync(new StartCounterOrderDto
            {
                CustomerName = "Ana",
                Phone = "12",
            }));
    }

    [Fact]
    public async Task Counter_tenant_cannot_open_a_table_or_delivery_order()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var tableId = Guid.NewGuid();
        fx.Db.DiningTables.Add(new DiningTable
        {
            Id = tableId,
            TenantId = fx.TenantId,
            Code = "M1",
            Capacity = 2,
            Status = ETableStatus.Available,
            IsActive = true,
        });
        await fx.Db.SaveChangesAsync();
        var sut = CreateSut(fx);

        var table = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.StartOrderForTableAsync(tableId));
        var delivery = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.StartDeliveryOrderAsync(new Restaurant.Application.Features.Crm.Customers.StartDeliveryCustomerDto
            {
                TaxId = "555",
                Name = "Luis",
                Phone = "301",
                Address = "Calle 1",
            }));

        Assert.Contains("mostrador", table.Message);
        Assert.Contains("mostrador", delivery.Message);
    }

    [Fact]
    public async Task Paid_table_order_still_cannot_be_sent_to_the_kitchen()
    {
        using var fx = new TenantDbFixture();
        var orderId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        fx.Db.DiningTables.Add(new DiningTable
        {
            Id = tableId,
            TenantId = fx.TenantId,
            Code = "M1",
            Capacity = 2,
            Status = ETableStatus.Busy,
            IsActive = true,
        });
        fx.Db.SalesOrders.Add(new SalesOrder
        {
            Id = orderId,
            TenantId = fx.TenantId,
            DiningTableId = tableId,
            Number = "T-1",
            Status = SalesOrderStatus.Paid,
            OpenedAtUtc = DateTime.UtcNow,
        });
        await fx.Db.SaveChangesAsync();
        var sut = CreateSut(fx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ConfirmOrderAsync(orderId, new ConfirmSalesOrderDto()));

        Assert.Equal("Paid or voided orders cannot be modified.", ex.Message);
    }

    [Fact]
    public async Task Counter_order_is_sent_to_the_kitchen_only_after_payment()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var productId = await AddProductAsync(fx);
        var sut = CreateSut(fx);
        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            Phone = "3001234567",
        });

        var unpaid = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ConfirmOrderAsync(order.Id, new ConfirmSalesOrderDto()));
        Assert.Contains("Cobre el pedido", unpaid.Message);

        await sut.AddLineAsync(order.Id, new AddSalesOrderLineDto
        {
            ProductId = productId,
            Quantity = 1,
        });
        var stored = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        stored.Status = SalesOrderStatus.Paid;
        await fx.Db.SaveChangesAsync();

        var sent = await sut.ConfirmOrderAsync(order.Id, new ConfirmSalesOrderDto());

        Assert.NotNull(sent);
        Assert.Equal(SalesOrderStatus.Paid, sent.Order.Status);
        Assert.NotNull(sent.Order.Lines.Single().SentToKitchenAtUtc);
        Assert.Null(sent.Order.DiningTableId);
    }

    [Fact]
    public async Task Ready_notice_failure_does_not_block_pickup_and_can_be_retried()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var productId = await AddProductAsync(fx);
        var notifier = new ScriptedNotifier();
        notifier.Results.Enqueue(new CounterReadyNotificationResult(false, "WhatsApp no está configurado."));
        notifier.Results.Enqueue(new CounterReadyNotificationResult(true, null));
        var sut = CreateSut(fx, notifier);
        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            Phone = "3001234567",
        });
        await sut.AddLineAsync(order.Id, new AddSalesOrderLineDto { ProductId = productId, Quantity = 1 });
        var stored = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        stored.Status = SalesOrderStatus.Paid;
        await fx.Db.SaveChangesAsync();
        await sut.ConfirmOrderAsync(order.Id, new ConfirmSalesOrderDto());

        var ready = await sut.MarkCounterReadyAsync(order.Id);
        Assert.NotNull(ready);
        Assert.NotNull(ready.CounterReadyAtUtc);
        Assert.Null(ready.CounterNotifiedAtUtc);
        Assert.Equal("WhatsApp no está configurado.", ready.CounterNotificationError);
        Assert.Equal(SalesOrderStatus.Paid, ready.Status);

        var retried = await sut.MarkCounterReadyAsync(order.Id);
        Assert.NotNull(retried);
        Assert.NotNull(retried.CounterNotifiedAtUtc);
        Assert.Null(retried.CounterNotificationError);
        Assert.Equal(ready.CounterReadyAtUtc, retried.CounterReadyAtUtc);
        Assert.Equal(2, notifier.Calls);
    }

    [Fact]
    public async Task Handover_closes_pickup_and_abandon_keeps_the_paid_sale()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var productId = await AddProductAsync(fx);
        var sut = CreateSut(fx, new ScriptedNotifier());
        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            Phone = "3001234567",
        });
        await sut.AddLineAsync(order.Id, new AddSalesOrderLineDto { ProductId = productId, Quantity = 1 });
        var stored = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        stored.Status = SalesOrderStatus.Paid;
        await fx.Db.SaveChangesAsync();
        await sut.ConfirmOrderAsync(order.Id, new ConfirmSalesOrderDto());

        var tooSoon = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.HandOverCounterOrderAsync(order.Id));
        Assert.Contains("listo", tooSoon.Message);

        await sut.MarkCounterReadyAsync(order.Id);
        var handed = await sut.HandOverCounterOrderAsync(order.Id);
        Assert.NotNull(handed?.CounterHandedOverAtUtc);
        Assert.Equal(SalesOrderStatus.Paid, handed.Status);
        Assert.Empty(await sut.ListCounterOrdersAsync());

        var other = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Luis",
            Phone = "3011234567",
        });
        await sut.AddLineAsync(other.Id, new AddSalesOrderLineDto { ProductId = productId, Quantity = 1 });
        var otherStored = await fx.Db.SalesOrders.SingleAsync(o => o.Id == other.Id);
        otherStored.Status = SalesOrderStatus.Paid;
        await fx.Db.SaveChangesAsync();

        var abandoned = await sut.AbandonCounterOrderAsync(other.Id);
        Assert.NotNull(abandoned?.CounterAbandonedAtUtc);
        Assert.Equal(SalesOrderStatus.Paid, abandoned.Status);
        Assert.DoesNotContain(await sut.ListCounterOrdersAsync(), item => item.Id == other.Id);
    }

    [Fact]
    public async Task Paying_a_counter_order_sends_it_to_the_kitchen()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var productId = await AddProductAsync(fx);
        var sut = CreateSut(fx);
        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            Phone = "3001234567",
        });
        await sut.AddLineAsync(order.Id, new AddSalesOrderLineDto { ProductId = productId, Quantity = 1 });
        var stored = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        stored.Status = SalesOrderStatus.Paid;
        await fx.Db.SaveChangesAsync();

        await sut.DispatchPaidCounterOrdersToKitchenAsync([order.Id, Guid.NewGuid()]);

        var sent = await fx.Db.SalesOrderLines.SingleAsync(l => l.SalesOrderId == order.Id);
        Assert.NotNull(sent.SentToKitchenAtUtc);
        Assert.Equal(SalesOrderStatus.Paid, (await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id)).Status);
        var listed = Assert.Single(await sut.ListCounterOrdersAsync());
        Assert.True(listed.SentToKitchen);
        Assert.Equal(sent.SentToKitchenAtUtc, listed.SentToKitchenAtUtc);
    }

    [Fact]
    public async Task An_unpaid_counter_order_is_not_sent_when_dispatch_runs()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var productId = await AddProductAsync(fx);
        var sut = CreateSut(fx);
        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            Phone = "3001234567",
        });
        await sut.AddLineAsync(order.Id, new AddSalesOrderLineDto { ProductId = productId, Quantity = 1 });

        await sut.DispatchPaidCounterOrdersToKitchenAsync([order.Id]);

        var line = await fx.Db.SalesOrderLines.SingleAsync(l => l.SalesOrderId == order.Id);
        Assert.Null(line.SentToKitchenAtUtc);
        Assert.Equal(SalesOrderStatus.Draft, (await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id)).Status);
        var listed = Assert.Single(await sut.ListCounterOrdersAsync());
        Assert.False(listed.SentToKitchen);
        Assert.Null(listed.SentToKitchenAtUtc);
    }

    [Fact]
    public async Task A_failed_kitchen_ticket_keeps_the_paid_counter_order_unsent()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var settings = await fx.Db.TenantSettings.SingleAsync();
        settings.KitchenTicketPrintMode = KitchenTicketPrintMode.Always;
        await fx.Db.SaveChangesAsync();
        var productId = await AddProductAsync(fx);
        var sut = CreateSut(fx);
        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            Phone = "3001234567",
        });
        await sut.AddLineAsync(order.Id, new AddSalesOrderLineDto { ProductId = productId, Quantity = 1 });
        var stored = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        stored.Status = SalesOrderStatus.Paid;
        await fx.Db.SaveChangesAsync();

        await sut.DispatchPaidCounterOrdersToKitchenAsync([order.Id]);

        var line = await fx.Db.SalesOrderLines.SingleAsync(l => l.SalesOrderId == order.Id);
        Assert.Null(line.SentToKitchenAtUtc);
        Assert.Equal(SalesOrderStatus.Paid, stored.Status);
    }

    [Fact]
    public async Task Dispatch_leaves_a_paid_table_order_untouched()
    {
        using var fx = new TenantDbFixture();
        var productId = await AddProductAsync(fx);
        var sentAt = DateTime.UtcNow;
        var orderId = Guid.NewGuid();
        fx.Db.SalesOrders.Add(new SalesOrder
        {
            Id = orderId,
            TenantId = fx.TenantId,
            Number = "T-1",
            Status = SalesOrderStatus.Paid,
            OpenedAtUtc = sentAt,
        });
        fx.Db.SalesOrderLines.Add(new SalesOrderLine
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            SalesOrderId = orderId,
            ProductId = productId,
            Quantity = 1,
            UnitPrice = 8_000,
            LineTotal = 8_000,
            SentToKitchenAtUtc = sentAt,
        });
        await fx.Db.SaveChangesAsync();
        var sut = CreateSut(fx);

        await sut.DispatchPaidCounterOrdersToKitchenAsync([orderId]);

        var line = await fx.Db.SalesOrderLines.SingleAsync(l => l.SalesOrderId == orderId);
        Assert.NotNull(line.SentToKitchenAtUtc);
        Assert.Equal(SalesOrderStatus.Paid, (await fx.Db.SalesOrders.SingleAsync(o => o.Id == orderId)).Status);
    }

    [Fact]
    public async Task Counter_order_cannot_be_moved_to_a_table()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var sut = CreateSut(fx);
        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            Phone = "3001234567",
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RelocateOrderAsync(order.Id, new RelocateOrderDto { TargetTableId = Guid.NewGuid() }));

        Assert.Contains("no se asigna a una mesa", ex.Message);
    }

    [Fact]
    public async Task A_pager_order_stores_the_puck_and_does_not_require_a_phone()
    {
        using var fx = new TenantDbFixture();
        EnablePager(fx);
        var sut = CreateSut(fx);

        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            PagerNumber = "007",
        });

        Assert.True(order.CounterUsesPager);
        Assert.Equal("7", order.CounterPagerNumber);
        Assert.Null(order.CounterPhone);
    }

    [Fact]
    public async Task A_pager_number_is_free_again_after_the_order_is_handed_over()
    {
        using var fx = new TenantDbFixture();
        EnablePager(fx);
        var sut = CreateSut(fx);
        var first = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            PagerNumber = "14",
        });

        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.StartCounterOrderAsync(new StartCounterOrderDto
            {
                CustomerName = "Luis",
                PagerNumber = "14",
            }));
        Assert.Contains("ya está", duplicate.Message);

        var stored = await fx.Db.SalesOrders.SingleAsync(o => o.Id == first.Id);
        stored.Status = SalesOrderStatus.Paid;
        stored.CounterReadyAtUtc = DateTime.UtcNow;
        await fx.Db.SaveChangesAsync();
        await sut.HandOverCounterOrderAsync(first.Id);

        var again = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Luis",
            PagerNumber = "14",
        });
        Assert.Equal("14", again.CounterPagerNumber);
    }

    [Fact]
    public async Task Listo_on_a_pager_order_does_not_send_WhatsApp()
    {
        using var fx = new TenantDbFixture();
        EnablePager(fx);
        var productId = await AddProductAsync(fx);
        var notifier = new ScriptedNotifier();
        var sut = CreateSut(fx, notifier);
        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            PagerNumber = "14",
        });
        await sut.AddLineAsync(order.Id, new AddSalesOrderLineDto { ProductId = productId, Quantity = 1 });
        var stored = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        stored.Status = SalesOrderStatus.Paid;
        await fx.Db.SaveChangesAsync();
        await sut.DispatchPaidCounterOrdersToKitchenAsync([order.Id]);

        var ready = await sut.MarkCounterReadyAsync(order.Id);

        Assert.NotNull(ready?.CounterReadyAtUtc);
        Assert.Null(ready.CounterNotificationError);
        Assert.Null(ready.CounterNotifiedAtUtc);
        Assert.Equal(0, notifier.Calls);
    }

    [Fact]
    public async Task An_order_opened_with_WhatsApp_still_notifies_after_the_tenant_switches_to_a_pager()
    {
        using var fx = new TenantDbFixture();
        EnableCounter(fx);
        var productId = await AddProductAsync(fx);
        var notifier = new ScriptedNotifier();
        notifier.Results.Enqueue(new CounterReadyNotificationResult(true, null));
        var sut = CreateSut(fx, notifier);
        var order = await sut.StartCounterOrderAsync(new StartCounterOrderDto
        {
            CustomerName = "Ana",
            Phone = "3001234567",
        });
        await sut.AddLineAsync(order.Id, new AddSalesOrderLineDto { ProductId = productId, Quantity = 1 });
        var stored = await fx.Db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        stored.Status = SalesOrderStatus.Paid;
        await fx.Db.SaveChangesAsync();
        await sut.ConfirmOrderAsync(order.Id, new ConfirmSalesOrderDto());
        var settings = await fx.Db.TenantSettings.SingleAsync();
        settings.CounterUsesPager = true;
        await fx.Db.SaveChangesAsync();

        var ready = await sut.MarkCounterReadyAsync(order.Id);

        Assert.NotNull(ready?.CounterNotifiedAtUtc);
        Assert.Equal(1, notifier.Calls);
        Assert.False(ready.CounterUsesPager);
    }

    [Theory]
    [InlineData("3001234567", "573001234567")]
    [InlineData("573001234567", "573001234567")]
    [InlineData("123", null)]
    public void WhatsApp_recipient_accepts_a_colombian_mobile(string raw, string? expected)
    {
        Assert.Equal(expected, CounterPhoneNumbers.ToWhatsAppRecipient(raw));
    }

    private static void EnablePager(TenantDbFixture fx)
    {
        EnableCounter(fx);
        var settings = fx.Db.TenantSettings.Single();
        settings.CounterUsesPager = true;
        fx.Db.SaveChanges();
    }

    private static void EnableCounter(TenantDbFixture fx)
    {
        fx.Db.TenantSettings.Add(new TenantSettings
        {
            TenantId = fx.TenantId,
            CounterServiceEnabled = true,
            TradeName = "Plaza",
            KitchenTicketPrintMode = KitchenTicketPrintMode.Never,
        });
        fx.Db.SaveChanges();
    }

    private static async Task<Guid> AddProductAsync(TenantDbFixture fx)
    {
        var typeId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(new ProductType
        {
            Id = typeId,
            TenantId = fx.TenantId,
            Name = "Platos",
            IsActive = true,
        });
        fx.Db.Products.Add(new Product
        {
            Id = productId,
            TenantId = fx.TenantId,
            ProductTypeId = typeId,
            Name = "Arepa",
            UnitPrice = 8_000,
            CompositionType = EProductType.Resale,
            IsActive = true,
        });
        await fx.Db.SaveChangesAsync();
        return productId;
    }

    private static SalesOrderService CreateSut(TenantDbFixture fx, ICounterReadyNotifier? notifier = null)
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
            customers,
            counterNotifier: notifier);
    }

    private sealed class ScriptedNotifier : ICounterReadyNotifier
    {
        public Queue<CounterReadyNotificationResult> Results { get; } = new();
        public int Calls { get; private set; }

        public Task<CounterReadyNotificationResult> NotifyReadyAsync(
            SalesOrder order,
            string tradeName,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Results.Count == 0
                ? new CounterReadyNotificationResult(false, "WhatsApp no está configurado.")
                : Results.Dequeue());
        }
    }
}
