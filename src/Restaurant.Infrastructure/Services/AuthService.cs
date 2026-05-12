using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Auth;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Authorization;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(ApplicationDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponseDto> RegisterTenantAsync(RegisterTenantDto dto, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(dto.AdminEmail);
        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
            throw new InvalidOperationException("Email is already registered.");

        var slugBase = Slugify(dto.TenantName);
        var slug = await EnsureUniqueSlugAsync(slugBase, cancellationToken);

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
            NewRole(tenant.Id, SystemRoles.Owner),
            NewRole(tenant.Id, SystemRoles.Manager),
            NewRole(tenant.Id, SystemRoles.Staff),
        };

        var tenantUser = new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
        };

        var ownerRole = roles.First(r => r.Name == SystemRoles.Owner);
        tenantUser.TenantUserRoles.Add(new TenantUserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            TenantUserId = tenantUser.Id,
            RoleId = ownerRole.Id,
        });

        await _db.Tenants.AddAsync(tenant, cancellationToken);
        await _db.Users.AddAsync(user, cancellationToken);
        foreach (var role in roles)
            await _db.Roles.AddAsync(role, cancellationToken);
        await _db.TenantUsers.AddAsync(tenantUser, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenService.CreateAccessToken(user.Id, tenant.Id, user.Email, new[] { SystemRoles.Owner });
        return new AuthResponseDto
        {
            AccessToken = token,
            UserId = user.Id,
            TenantId = tenant.Id,
            TenantSlug = tenant.Slug,
            Email = user.Email,
            Roles = new[] { SystemRoles.Owner },
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null || !_passwordHasher.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var memberships = await _db.TenantUsers
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
        var token = _jwtTokenService.CreateAccessToken(user.Id, membership.TenantId, user.Email, roles);
        return new AuthResponseDto
        {
            AccessToken = token,
            UserId = user.Id,
            TenantId = membership.TenantId,
            TenantSlug = membership.Tenant.Slug,
            Email = user.Email,
            Roles = roles,
        };
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

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, CancellationToken cancellationToken)
    {
        var slug = baseSlug;
        var i = 0;
        while (await _db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
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
