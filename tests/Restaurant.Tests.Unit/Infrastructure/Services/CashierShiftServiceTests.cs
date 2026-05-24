using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Cashier;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Identity;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class CashierShiftServiceTests
{
    private static async Task SeedTenantUserAsync(TenantDbFixture fx, Guid userId, string email)
    {
        await fx.Db.Tenants.AddAsync(new Tenant
        {
            Id = fx.TenantId,
            Name = "Test",
            Slug = "test",
            TimeZoneId = "America/Bogota",
            IsActive = true,
        });
        await fx.Db.Users.AddAsync(new User
        {
            Id = userId,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = "hash",
        });
        await fx.Db.TenantSettings.AddAsync(new TenantSettings
        {
            TenantId = fx.TenantId,
            MaxDiscountPercent = 10m,
            OperationalDayCutoffHour = 4,
        });
        await fx.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task OpenShiftAsync_does_not_duplicate_user()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        await SeedTenantUserAsync(fx, userId, "cashier@test.local");

        var operationalDay = new OperationalBusinessDayService(fx.Db, fx.TenantContext);
        var sut = new CashierShiftService(fx.Db, fx.TenantContext, operationalDay);
        var shift = await sut.OpenShiftAsync(new OpenCashierShiftDto { OpeningFloat = 100m });

        Assert.Equal("cashier@test.local", shift.CashierEmail);
        Assert.Equal(1, await fx.Db.Users.CountAsync());
        Assert.Equal(1, await fx.Db.CashierShifts.CountAsync());
    }
}
