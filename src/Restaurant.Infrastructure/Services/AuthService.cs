using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Auth;
using Restaurant.Domain.Entities;
using Restaurant.Application.Authorization;
using Restaurant.Infrastructure.Authorization;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Infrastructure.Persistence.Seeding;

namespace Restaurant.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRolePermissionService _rolePermissions;
    private readonly ICurrentTenantContext _tenantContext;

    public AuthService(
        IUnitOfWork unitOfWork,
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRolePermissionService rolePermissions,
        ICurrentTenantContext tenantContext)
    {
        _unitOfWork = unitOfWork;
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _rolePermissions = rolePermissions;
        _tenantContext = tenantContext;
    }

    public async Task<AuthResponseDto> RegisterTenantAsync(RegisterTenantDto dto, CancellationToken cancellationToken = default)
    {
        var users = _unitOfWork.Repository<User>();
        var tenants = _unitOfWork.Repository<Tenant>();
        var rolesRepo = _unitOfWork.Repository<Role>();
        var tenantUsers = _unitOfWork.Repository<TenantUser>();

        var normalizedEmail = NormalizeEmail(dto.AdminEmail);
        if (await users.Query().AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
            throw new InvalidOperationException("Email is already registered.");

        var slugBase = Slugify(dto.TenantName);
        var slug = await EnsureUniqueSlugAsync(tenants, slugBase, cancellationToken);

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = dto.TenantName.Trim(),
            Slug = slug,
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.AdminEmail.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(dto.Password),
            DisplayName = dto.AdminDisplayName?.Trim(),
        };

        var roles = new[]
        {
            NewRole(tenant.Id, SystemRoles.Administrator),
            NewRole(tenant.Id, SystemRoles.Manager),
            NewRole(tenant.Id, SystemRoles.Waitress),
            NewRole(tenant.Id, SystemRoles.Cashier),
        };

        var tenantUser = new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
        };

        var adminRole = roles.First(r => r.Name == SystemRoles.Administrator);
        tenantUser.TenantUserRoles.Add(new TenantUserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            TenantUserId = tenantUser.Id,
            RoleId = adminRole.Id,
        });

        await tenants.AddAsync(tenant, cancellationToken);
        await users.AddAsync(user, cancellationToken);
        await rolesRepo.AddRangeAsync(roles, cancellationToken);
        await tenantUsers.AddAsync(tenantUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await IngredientMovementTypeBootstrap.EnsureForTenantAsync(_db, tenant.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _tenantContext.TenantId = tenant.Id;

        await _rolePermissions.UpdateRolePermissionsAsync(
            adminRole.Id,
            new Application.Features.Organization.RolePermissions.UpdateRolePermissionsDto
            {
                FeatureCodes = Application.Authorization.FeatureCodes.All,
            },
            cancellationToken);
        await AssignDefaultRolePermissionsAsync(tenant.Id, roles.Where(r => r.Id != adminRole.Id), cancellationToken);

        var permissions = await _rolePermissions.GetPermissionCodesForUserAsync(user.Id, tenant.Id, cancellationToken);
        var token = _jwtTokenService.CreateAccessToken(
            user.Id,
            tenant.Id,
            user.Email,
            new[] { SystemRoles.Administrator },
            permissions);
        return new AuthResponseDto
        {
            AccessToken = token,
            UserId = user.Id,
            TenantId = tenant.Id,
            TenantSlug = tenant.Slug,
            Email = user.Email,
            Roles = new[] { SystemRoles.Administrator },
            Permissions = permissions,
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var users = _unitOfWork.Repository<User>();
        var tenantUsersRepo = _unitOfWork.Repository<TenantUser>();

        var normalizedEmail = NormalizeEmail(dto.Email);
        var user = await users.Query().AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null || !_passwordHasher.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var memberships = await tenantUsersRepo.Query()
            .AsNoTracking()
            .Include(tu => tu.Tenant)
            .Include(tu => tu.TenantUserRoles).ThenInclude(tur => tur.Role)
            .Where(tu => tu.UserId == user.Id && tu.IsActive && tu.Tenant.IsActive)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
            throw new UnauthorizedAccessException("No active tenant memberships.");

        TenantUser membership;
        if (!string.IsNullOrWhiteSpace(dto.TenantSlug))
        {
            var slug = dto.TenantSlug.Trim().ToLowerInvariant();
            membership = memberships.FirstOrDefault(m => m.Tenant.Slug == slug)
                ?? throw new UnauthorizedAccessException("Tenant not found for this account.");
        }
        else if (memberships.Count == 1)
        {
            membership = memberships[0];
        }
        else
        {
            throw new InvalidOperationException(
                "Multiple tenants available; specify tenantSlug (GET /api/auth/tenants can list them).");
        }

        var roles = membership.TenantUserRoles.Select(tur => tur.Role.Name).Distinct().ToList();
        var permissions = await _rolePermissions.GetPermissionCodesForUserAsync(
            user.Id,
            membership.TenantId,
            cancellationToken);
        var token = _jwtTokenService.CreateAccessToken(
            user.Id,
            membership.TenantId,
            user.Email,
            roles,
            permissions);
        return new AuthResponseDto
        {
            AccessToken = token,
            UserId = user.Id,
            TenantId = membership.TenantId,
            TenantSlug = membership.Tenant.Slug,
            Email = user.Email,
            Roles = roles,
            Permissions = permissions,
        };
    }

    private async Task AssignDefaultRolePermissionsAsync(
        Guid tenantId,
        IEnumerable<Role> roles,
        CancellationToken cancellationToken)
    {
        foreach (var role in roles)
        {
            if (!FeatureCatalog.DefaultFeaturesByRole.TryGetValue(role.Name, out var codes))
                continue;

            await _rolePermissions.UpdateRolePermissionsAsync(
                role.Id,
                new Application.Features.Organization.RolePermissions.UpdateRolePermissionsDto { FeatureCodes = codes },
                cancellationToken);
        }
    }

    private static Role NewRole(Guid tenantId, string name)
    {
        var normalized = name.Trim().ToUpperInvariant();
        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            NormalizedName = normalized,
        };
    }

    private static async Task<string> EnsureUniqueSlugAsync(
        IRepository<Tenant> tenants,
        string baseSlug,
        CancellationToken cancellationToken)
    {
        var slug = baseSlug;
        var i = 0;
        while (await tenants.Query().AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            i++;
            slug = $"{baseSlug}-{i}";
        }

        return slug;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static string Slugify(string name)
    {
        var lower = name.Trim().ToLowerInvariant();
        var slug = Regex.Replace(lower, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "tenant" : slug;
    }
}
