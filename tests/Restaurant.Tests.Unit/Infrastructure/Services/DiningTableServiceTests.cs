using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class DiningTableServiceTests
{
    [Fact]
    public async Task SetStatusAsync_allows_available_to_busy()
    {
        using var fx = new TenantDbFixture();
        var tableId = Guid.NewGuid();
        fx.Db.DiningTables.Add(
            new DiningTable
            {
                Id = tableId,
                TenantId = fx.TenantId,
                Code = "T-1",
                Capacity = 4,
                Status = ETableStatus.Available,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new DiningTableService(fx.Repository<DiningTable>(), fx.UnitOfWork, fx.Mapper);
        var updated = await sut.SetStatusAsync(tableId, ETableStatus.Busy);

        Assert.NotNull(updated);
        Assert.Equal(ETableStatus.Busy, updated.Status);
    }

    [Fact]
    public async Task SetStatusAsync_rejects_busy_to_reserved()
    {
        using var fx = new TenantDbFixture();
        var tableId = Guid.NewGuid();
        fx.Db.DiningTables.Add(
            new DiningTable
            {
                Id = tableId,
                TenantId = fx.TenantId,
                Code = "T-2",
                Capacity = 2,
                Status = ETableStatus.Busy,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new DiningTableService(fx.Repository<DiningTable>(), fx.UnitOfWork, fx.Mapper);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetStatusAsync(tableId, ETableStatus.Reserved));
    }
}
