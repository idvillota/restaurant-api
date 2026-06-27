using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Restaurant.Domain.Entities;

namespace Restaurant.Infrastructure.Persistence.Seeding;

public static class IngredientMovementTypeBootstrap
{
    public static async Task EnsureAsync(ApplicationDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        var tenantIds = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var added = 0;
        foreach (var tenantId in tenantIds)
            added += await EnsureForTenantAsync(db, tenantId, cancellationToken);

        if (added > 0)
            await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Default ingredient movement types ensured for {TenantCount} tenant(s); {Added} row(s) inserted.",
            tenantIds.Count,
            added);
    }

    public static async Task<int> EnsureForTenantAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var existingNames = await db.IngredientMovementTypes.IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.Name)
            .ToListAsync(cancellationToken);
        var existingSet = existingNames.ToHashSet(StringComparer.Ordinal);

        var added = 0;
        foreach (var def in DefaultIngredientMovementTypes.All)
        {
            if (existingSet.Contains(def.Name))
                continue;

            await db.IngredientMovementTypes.AddAsync(
                new IngredientMovementType
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = def.Name,
                    Description = def.Description,
                    IsInput = def.IsInput,
                    SortOrder = def.SortOrder,
                    IsActive = true,
                },
                cancellationToken);
            existingSet.Add(def.Name);
            added++;
        }

        return added;
    }
}
