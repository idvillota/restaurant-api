using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Reservations;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class ReservationServiceTests
{
    [Fact]
    public async Task CreateAsync_throws_when_end_before_start()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);
        var start = DateTime.UtcNow;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(
                new CreateReservationDto
                {
                    ContactName = "Guest",
                    PartySize = 2,
                    StartAtUtc = start,
                    EndAtUtc = start.AddMinutes(-30),
                }));
    }

    [Fact]
    public async Task DeleteAsync_removes_entity()
    {
        using var fx = new TenantDbFixture();
        var id = Guid.NewGuid();
        fx.Db.Reservations.Add(
            new Reservation
            {
                Id = id,
                TenantId = fx.TenantId,
                ContactName = "Walk-in",
                PartySize = 1,
                StartAtUtc = DateTime.UtcNow,
                EndAtUtc = DateTime.UtcNow.AddHours(1),
                Status = ReservationStatus.Pending,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);

        var ok = await sut.DeleteAsync(id);
        Assert.True(ok);
        Assert.Equal(0, await fx.Db.Reservations.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_reserves_available_table()
    {
        using var fx = new TenantDbFixture();
        var tableId = Guid.NewGuid();
        fx.Db.DiningTables.Add(
            new DiningTable
            {
                Id = tableId,
                TenantId = fx.TenantId,
                Code = "P-1",
                Capacity = 4,
                Status = ETableStatus.Available,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        var start = DateTime.UtcNow.AddHours(1);
        var created = await sut.CreateAsync(
            new CreateReservationDto
            {
                ContactName = "Party",
                PartySize = 4,
                StartAtUtc = start,
                EndAtUtc = start.AddHours(2),
                DiningTableIds = [tableId],
            });

        Assert.Contains(tableId, created.DiningTableIds);
        var table = await fx.Db.DiningTables.FindAsync(tableId);
        Assert.Equal(ETableStatus.Reserved, table!.Status);
    }

    [Fact]
    public async Task CreateAsync_throws_when_table_is_busy()
    {
        using var fx = new TenantDbFixture();
        var tableId = Guid.NewGuid();
        fx.Db.DiningTables.Add(
            new DiningTable
            {
                Id = tableId,
                TenantId = fx.TenantId,
                Code = "P-2",
                Capacity = 2,
                Status = ETableStatus.Busy,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        var start = DateTime.UtcNow.AddHours(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(
                new CreateReservationDto
                {
                    ContactName = "Party",
                    PartySize = 2,
                    StartAtUtc = start,
                    EndAtUtc = start.AddHours(2),
                    DiningTableIds = [tableId],
                }));
    }

    [Fact]
    public async Task UpdateAsync_to_completed_releases_table()
    {
        using var fx = new TenantDbFixture();
        var reservationId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(1);
        fx.Db.Reservations.Add(
            new Reservation
            {
                Id = reservationId,
                TenantId = fx.TenantId,
                ContactName = "Dine",
                PartySize = 2,
                StartAtUtc = start,
                EndAtUtc = start.AddHours(2),
                Status = ReservationStatus.Seated,
            });
        fx.Db.DiningTables.Add(
            new DiningTable
            {
                Id = tableId,
                TenantId = fx.TenantId,
                Code = "P-3",
                Capacity = 2,
                Status = ETableStatus.Busy,
                IsActive = true,
            });
        fx.Db.ReservationTables.Add(
            new ReservationTable
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ReservationId = reservationId,
                DiningTableId = tableId,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        await sut.UpdateAsync(
            reservationId,
            new UpdateReservationDto
            {
                ContactName = "Dine",
                PartySize = 2,
                StartAtUtc = start,
                EndAtUtc = start.AddHours(2),
                Status = ReservationStatus.Completed,
            });

        var table = await fx.Db.DiningTables.FindAsync(tableId);
        Assert.Equal(ETableStatus.Available, table!.Status);
    }

    private static ReservationService CreateSut(TenantDbFixture fx) =>
        new(
            fx.Repository<Reservation>(),
            fx.Repository<Customer>(),
            fx.Repository<DiningTable>(),
            fx.Repository<ReservationTable>(),
            fx.UnitOfWork,
            fx.Mapper);
}
