using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Authorization;

namespace Restaurant.Infrastructure.Persistence.Seeding;

public static class PermissionBootstrap
{
    public static async Task EnsureAsync(ApplicationDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await EnsureFeaturesAsync(db, cancellationToken);

        var tenantIds = await db.Tenants.IgnoreQueryFilters().Where(t => t.IsActive).Select(t => t.Id).ToListAsync(cancellationToken);
        foreach (var tenantId in tenantIds)
        {
            await EnsureTenantRolesAsync(db, tenantId, cancellationToken);
            await EnsureDefaultRoleFeaturesAsync(db, tenantId, cancellationToken);
        }

        logger.LogInformation("Permission catalog and role defaults ensured for {TenantCount} tenant(s).", tenantIds.Count);
    }

    private static async Task EnsureFeaturesAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Features.IgnoreQueryFilters().ToDictionaryAsync(f => f.Code, cancellationToken);

        foreach (var def in FeatureCatalog.All)
        {
            if (existing.ContainsKey(def.Code))
                continue;

            await db.Features.AddAsync(
                new Feature
                {
                    Id = def.Id,
                    Code = def.Code,
                    Name = def.Name,
                    Module = def.Module,
                    SortOrder = def.SortOrder,
                },
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureTenantRolesAsync(ApplicationDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var roles = await db.Roles.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).ToListAsync(cancellationToken);

        RenameLegacyRole(roles, SystemRoles.Owner, SystemRoles.Administrator);
        RenameLegacyRole(roles, SystemRoles.Staff, SystemRoles.Waitress);

        var existingNames = roles.Select(r => r.NormalizedName).ToHashSet(StringComparer.Ordinal);

        foreach (var roleName in SystemRoles.All)
        {
            var normalized = roleName.ToUpperInvariant();
            if (existingNames.Contains(normalized))
                continue;

            await db.Roles.AddAsync(
                new Role
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = roleName,
                    NormalizedName = normalized,
                },
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void RenameLegacyRole(List<Role> roles, string legacyName, string newName)
    {
        var legacy = roles.FirstOrDefault(r => r.Name.Equals(legacyName, StringComparison.OrdinalIgnoreCase));
        var target = roles.FirstOrDefault(r => r.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
        if (legacy is null || target is not null)
            return;

        legacy.Name = newName;
        legacy.NormalizedName = newName.ToUpperInvariant();
    }

    private static async Task EnsureDefaultRoleFeaturesAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var featuresByCode = await db.Features.IgnoreQueryFilters().ToDictionaryAsync(f => f.Code, cancellationToken);
        var roles = await db.Roles.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            var hasAny = await db.RoleFeatures.IgnoreQueryFilters()
                .AnyAsync(rf => rf.TenantId == tenantId && rf.RoleId == role.Id, cancellationToken);
            if (hasAny)
                continue;

            if (!FeatureCatalog.DefaultFeaturesByRole.TryGetValue(role.Name, out var codes))
                continue;

            foreach (var code in codes)
            {
                if (!featuresByCode.TryGetValue(code, out var feature))
                    continue;

                await db.RoleFeatures.AddAsync(
                    new RoleFeature
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        RoleId = role.Id,
                        FeatureId = feature.Id,
                    },
                    cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
