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
        var sut = new ReservationService(
            fx.Repository<Reservation>(),
            fx.Repository<Customer>(),
            fx.UnitOfWork,
            fx.Mapper);
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

        var sut = new ReservationService(
            fx.Repository<Reservation>(),
            fx.Repository<Customer>(),
            fx.UnitOfWork,
            fx.Mapper);

        var ok = await sut.DeleteAsync(id);
        Assert.True(ok);
        Assert.Equal(0, await fx.Db.Reservations.CountAsync());
    }
}
