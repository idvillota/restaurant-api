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

public sealed class SalesOrderRelocateTests
{
    [Fact]
    public async Task Relocate_to_free_table_transfers_order_and_frees_source()
    {
        using var fx = new TenantDbFixture();
        var (sourceId, targetId, orderId, sut) = await SeedTwoTablesWithSourceOrderAsync(fx);

        var result = await sut.RelocateOrderAsync(
            orderId,
            new RelocateOrderDto { TargetTableId = targetId });

        Assert.NotNull(result);
        Assert.Equal(RelocateOrderActions.Transferred, result.Action);
        Assert.Equal(targetId, result.Order!.DiningTableId);

        var source = await fx.Db.DiningTables.SingleAsync(t => t.Id == sourceId);
        var target = await fx.Db.DiningTables.SingleAsync(t => t.Id == targetId);
        Assert.Equal(ETableStatus.Available, source.Status);
        Assert.Equal(ETableStatus.Busy, target.Status);
    }

    [Fact]
    public async Task Relocate_to_busy_without_flag_returns_merge_required()
    {
        using var fx = new TenantDbFixture();
        var (_, targetId, sourceOrderId, targetOrderId, sut) = await SeedBusyTargetAsync(fx);

        var result = await sut.RelocateOrderAsync(
            sourceOrderId,
            new RelocateOrderDto { TargetTableId = targetId });

        Assert.NotNull(result);
        Assert.Equal(RelocateOrderActions.MergeRequired, result.Action);
        Assert.Equal(targetOrderId, result.TargetOrderId);

        var sourceStillOpen = await fx.Db.SalesOrders.CountAsync(o =>
            o.Id == sourceOrderId && o.Status == SalesOrderStatus.Draft);
        Assert.Equal(1, sourceStillOpen);
    }

    [Fact]
    public async Task Relocate_to_busy_with_false_cancels_without_changes()
    {
        using var fx = new TenantDbFixture();
        var (sourceId, targetId, sourceOrderId, _, sut) = await SeedBusyTargetAsync(fx);

        var result = await sut.RelocateOrderAsync(
            sourceOrderId,
            new RelocateOrderDto { TargetTableId = targetId, MergeIfTargetBusy = false });

        Assert.Equal(RelocateOrderActions.Cancelled, result!.Action);

        var source = await fx.Db.DiningTables.SingleAsync(t => t.Id == sourceId);
        Assert.Equal(ETableStatus.Busy, source.Status);
        var order = await fx.Db.SalesOrders.SingleAsync(o => o.Id == sourceOrderId);
        Assert.Equal(sourceId, order.DiningTableId);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);
    }

    [Fact]
    public async Task Relocate_to_busy_with_true_merges_lines_and_voids_source()
    {
        using var fx = new TenantDbFixture();
        var (sourceId, targetId, sourceOrderId, targetOrderId, sut) = await SeedBusyTargetAsync(fx);

        var result = await sut.RelocateOrderAsync(
            sourceOrderId,
            new RelocateOrderDto { TargetTableId = targetId, MergeIfTargetBusy = true });

        Assert.Equal(RelocateOrderActions.Merged, result!.Action);
        Assert.Equal(targetOrderId, result.Order!.Id);

        var sourceOrder = await fx.Db.SalesOrders.Include(o => o.Lines).SingleAsync(o => o.Id == sourceOrderId);
        Assert.Equal(SalesOrderStatus.Voided, sourceOrder.Status);
        Assert.Null(sourceOrder.DiningTableId);
        Assert.Empty(sourceOrder.Lines);

        var targetOrder = await fx.Db.SalesOrders.Include(o => o.Lines).SingleAsync(o => o.Id == targetOrderId);
        Assert.Equal(2, targetOrder.Lines.Count);

        var sourceTable = await fx.Db.DiningTables.SingleAsync(t => t.Id == sourceId);
        Assert.Equal(ETableStatus.Available, sourceTable.Status);
        var targetTable = await fx.Db.DiningTables.SingleAsync(t => t.Id == targetId);
        Assert.Equal(ETableStatus.Busy, targetTable.Status);
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
        }).CreateMapper();

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
            fx.Db);
    }

    private static async Task<(Guid SourceId, Guid TargetId, Guid OrderId, SalesOrderService Sut)>
        SeedTwoTablesWithSourceOrderAsync(TenantDbFixture fx)
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var typeId = Guid.NewGuid();

        fx.Db.DiningTables.AddRange(
            new DiningTable
            {
                Id = sourceId,
                TenantId = fx.TenantId,
                Code = "M1",
                Capacity = 4,
                Status = ETableStatus.Busy,
                IsActive = true,
            },
            new DiningTable
            {
                Id = targetId,
                TenantId = fx.TenantId,
                Code = "M5",
                Capacity = 4,
                Status = ETableStatus.Available,
                IsActive = true,
            });

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
            Name = "Pizza",
            UnitPrice = 10,
            CompositionType = EProductType.Resale,
            IsActive = true,
        });

        fx.Db.SalesOrders.Add(new SalesOrder
        {
            Id = orderId,
            TenantId = fx.TenantId,
            DiningTableId = sourceId,
            Number = "SO-1",
            Status = SalesOrderStatus.Draft,
            OpenedAtUtc = DateTime.UtcNow,
            Lines =
            {
                new SalesOrderLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = fx.TenantId,
                    SalesOrderId = orderId,
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = 10,
                    LineTotal = 10,
                },
            },
            Subtotal = 10,
            Total = 10,
        });

        await fx.Db.SaveChangesAsync();
        return (sourceId, targetId, orderId, CreateSut(fx));
    }

    private static async Task<(Guid SourceId, Guid TargetId, Guid SourceOrderId, Guid TargetOrderId, SalesOrderService Sut)>
        SeedBusyTargetAsync(TenantDbFixture fx)
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var sourceOrderId = Guid.NewGuid();
        var targetOrderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var typeId = Guid.NewGuid();

        fx.Db.DiningTables.AddRange(
            new DiningTable
            {
                Id = sourceId,
                TenantId = fx.TenantId,
                Code = "M1",
                Capacity = 4,
                Status = ETableStatus.Busy,
                IsActive = true,
            },
            new DiningTable
            {
                Id = targetId,
                TenantId = fx.TenantId,
                Code = "M5",
                Capacity = 4,
                Status = ETableStatus.Busy,
                IsActive = true,
            });

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
            Name = "Pizza",
            UnitPrice = 10,
            CompositionType = EProductType.Resale,
            IsActive = true,
        });

        fx.Db.SalesOrders.AddRange(
            new SalesOrder
            {
                Id = sourceOrderId,
                TenantId = fx.TenantId,
                DiningTableId = sourceId,
                Number = "SO-1",
                Status = SalesOrderStatus.Draft,
                OpenedAtUtc = DateTime.UtcNow,
                Lines =
                {
                    new SalesOrderLine
                    {
                        Id = Guid.NewGuid(),
                        TenantId = fx.TenantId,
                        SalesOrderId = sourceOrderId,
                        ProductId = productId,
                        Quantity = 1,
                        UnitPrice = 10,
                        LineTotal = 10,
                    },
                },
                Subtotal = 10,
                Total = 10,
            },
            new SalesOrder
            {
                Id = targetOrderId,
                TenantId = fx.TenantId,
                DiningTableId = targetId,
                Number = "SO-2",
                Status = SalesOrderStatus.Draft,
                OpenedAtUtc = DateTime.UtcNow,
                Lines =
                {
                    new SalesOrderLine
                    {
                        Id = Guid.NewGuid(),
                        TenantId = fx.TenantId,
                        SalesOrderId = targetOrderId,
                        ProductId = productId,
                        Quantity = 1,
                        UnitPrice = 10,
                        LineTotal = 10,
                    },
                },
                Subtotal = 10,
                Total = 10,
            });

        await fx.Db.SaveChangesAsync();
        return (sourceId, targetId, sourceOrderId, targetOrderId, CreateSut(fx));
    }
}
