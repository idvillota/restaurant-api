using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Organization.RolePermissions;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Authorization;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class RolePermissionService : IRolePermissionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;

    public RolePermissionService(ApplicationDbContext db, ICurrentTenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<RolePermissionMatrixDto> GetMatrixAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenant();

        var features = await _db.Features
            .AsNoTracking()
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .Select(f => new FeatureDto
            {
                Id = f.Id,
                Code = f.Code,
                Name = f.Name,
                Module = f.Module,
                SortOrder = f.SortOrder,
            })
            .ToListAsync(cancellationToken);

        var roles = await _db.Roles
            .AsNoTracking()
            .Where(r => SystemRoles.All.Contains(r.Name))
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var assignments = await _db.RoleFeatures
            .AsNoTracking()
            .Include(rf => rf.Feature)
            .ToListAsync(cancellationToken);

        var roleDtos = roles
            .Select(r => new RolePermissionRoleDto
            {
                Id = r.Id,
                Name = r.Name,
                FeatureCodes = assignments
                    .Where(a => a.RoleId == r.Id)
                    .Select(a => a.Feature.Code)
                    .OrderBy(c => c)
                    .ToList(),
            })
            .ToList();

        return new RolePermissionMatrixDto { Features = features, Roles = roleDtos };
    }

    public async Task UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsDto dto,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
            ?? throw new InvalidOperationException("Role was not found.");

        if (!SystemRoles.All.Contains(role.Name))
            throw new InvalidOperationException("Only system roles can be configured here.");

        var requested = dto.FeatureCodes.Distinct(StringComparer.Ordinal).ToList();
        var features = await _db.Features
            .AsNoTracking()
            .Where(f => requested.Contains(f.Code))
            .ToListAsync(cancellationToken);

        if (features.Count != requested.Count)
            throw new InvalidOperationException("One or more feature codes are invalid.");

        var existing = await _db.RoleFeatures.Where(rf => rf.RoleId == roleId).ToListAsync(cancellationToken);
        _db.RoleFeatures.RemoveRange(existing);

        foreach (var feature in features)
        {
            await _db.RoleFeatures.AddAsync(
                new RoleFeature
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantContext.TenantId!.Value,
                    RoleId = roleId,
                    FeatureId = feature.Id,
                },
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesForUserAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var roleIds = await _db.TenantUsers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(tu => tu.UserId == userId && tu.TenantId == tenantId && tu.IsActive)
            .SelectMany(tu => tu.TenantUserRoles.Select(tur => tur.RoleId))
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
            return [];

        return await _db.RoleFeatures
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(rf => rf.TenantId == tenantId && roleIds.Contains(rf.RoleId))
            .Select(rf => rf.Feature.Code)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }

    private void EnsureTenant()
    {
        if (!_tenantContext.TenantId.HasValue)
            throw new InvalidOperationException("Tenant context is required.");
    }
}
