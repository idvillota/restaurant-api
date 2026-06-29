using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Persistence.Seeding;

public static class KitchenPrinterBootstrap
{
    public static async Task EnsureAsync(ApplicationDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        var tenantIds = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var added = 0;
        foreach (var tenantId in tenantIds)
        {
            var exists = await db.PrinterStations.IgnoreQueryFilters()
                .AnyAsync(
                    s => s.TenantId == tenantId && s.Code == Domain.Common.KitchenPrinterDefaults.DefaultStationCode,
                    cancellationToken);
            if (exists)
                continue;

            await db.PrinterStations.AddAsync(
                new Domain.Entities.PrinterStation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = Domain.Common.KitchenPrinterDefaults.DefaultStationName,
                    Code = Domain.Common.KitchenPrinterDefaults.DefaultStationCode,
                    IsActive = true,
                    SortOrder = 0,
                },
                cancellationToken);
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Default kitchen printer stations ensured for {TenantCount} tenant(s); {Added} row(s) inserted.",
            tenantIds.Count,
            added);
    }
}
