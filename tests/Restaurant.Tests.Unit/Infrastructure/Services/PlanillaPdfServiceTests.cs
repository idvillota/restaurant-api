using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class PlanillaPdfServiceTests
{
    [Fact]
    public async Task GenerateDailyPlanilla_writes_pdf_under_tenant_Planillas_folder()
    {
        using var fx = new TenantDbFixture();
        var userId = Guid.NewGuid();
        fx.TenantContext.UserId = userId;
        var day = new DateOnly(2026, 11, 6);

        await fx.Db.Tenants.AddAsync(new Tenant
        {
            Id = fx.TenantId,
            Name = "Nuestro Café",
            Slug = "nuestro-cafe",
            TimeZoneId = "America/Bogota",
            IsActive = true,
        });
        await fx.Db.TenantSettings.AddAsync(new TenantSettings
        {
            TenantId = fx.TenantId,
            SeparateVitrinaCafeSales = true,
        });
        await fx.Db.Users.AddAsync(new User
        {
            Id = userId,
            Email = "admin@test.local",
            NormalizedEmail = "ADMIN@TEST.LOCAL",
            PasswordHash = "hash",
        });
        await fx.Db.DailyClosures.AddAsync(new DailyClosure
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            BusinessDate = day,
            Status = DailyClosureStatus.Closed,
            ClosedAtUtc = DateTime.UtcNow,
            ClosedByUserId = userId,
        });
        await fx.Db.SaveChangesAsync();

        var storage = new MemoryGeneratedFileStorage();
        var sut = new PlanillaPdfService(fx.Db, fx.TenantContext, storage);
        var result = await sut.GenerateDailyPlanillaAsync(day);

        Assert.StartsWith($"Planillas/{fx.TenantId:N}/planilla-{day:yyyy-MM-dd}", result.RelativePath);
        Assert.True(storage.Files.ContainsKey(result.RelativePath));
        Assert.True(storage.Files[result.RelativePath].Length > 100);
        Assert.Equal(0m, result.TotalSales);
        Assert.Equal(0m, result.TotalEgresos);
    }
}
