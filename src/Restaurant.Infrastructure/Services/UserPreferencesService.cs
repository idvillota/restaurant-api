using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.UserPreferences;
using Restaurant.Domain.Common;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class UserPreferencesService : IUserPreferencesService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;

    public UserPreferencesService(ApplicationDbContext db, ICurrentTenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<UserPreferencesDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var membership = await ResolveMembershipAsync(cancellationToken);
        return Map(membership);
    }

    public async Task<UserPreferencesDto> UpdateAsync(
        UpdateUserPreferencesDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!UserPreferences.IsValidBrandTheme(dto.BrandTheme))
            throw new ArgumentException("Invalid brand theme.");

        if (!UserPreferences.IsValidColorScheme(dto.ColorScheme))
            throw new ArgumentException("Invalid color scheme.");

        var membership = await ResolveMembershipAsync(cancellationToken);
        membership.BrandTheme = dto.BrandTheme;
        membership.ColorScheme = dto.ColorScheme;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(membership);
    }

    private async Task<TenantUser> ResolveMembershipAsync(CancellationToken cancellationToken)
    {
        if (!_tenantContext.TenantId.HasValue || !_tenantContext.UserId.HasValue)
            throw new UnauthorizedAccessException("Tenant context is required.");

        var membership = await _db.TenantUsers
            .FirstOrDefaultAsync(
                tu => tu.TenantId == _tenantContext.TenantId.Value
                    && tu.UserId == _tenantContext.UserId.Value
                    && tu.IsActive,
                cancellationToken);

        if (membership is null)
            throw new UnauthorizedAccessException("Active tenant membership not found.");

        return membership;
    }

    private static UserPreferencesDto Map(TenantUser membership) => new()
    {
        BrandTheme = UserPreferences.NormalizeBrandTheme(membership.BrandTheme),
        ColorScheme = UserPreferences.NormalizeColorScheme(membership.ColorScheme),
    };
}
