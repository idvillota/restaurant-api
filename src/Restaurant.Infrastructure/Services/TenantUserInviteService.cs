using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Organization.TenantUsers;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Authorization;

namespace Restaurant.Infrastructure.Services;

public sealed class TenantUserInviteService : ITenantUserInviteService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IPasswordHasher _passwordHasher;

    public TenantUserInviteService(
        IUnitOfWork unitOfWork,
        ICurrentTenantContext tenantContext,
        IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<InvitedTenantUserDto> InviteAsync(InviteTenantUserDto dto, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue)
            throw new InvalidOperationException("Tenant context is not set.");

        var tenantId = _tenantContext.TenantId.Value;
        var tenants = _unitOfWork.Repository<Tenant>();
        var tenant = await tenants.Query().AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        if (!tenant.IsActive)
            throw new InvalidOperationException("Tenant is not active.");

        var roleName = dto.Role.Trim();
        if (string.Equals(roleName, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleName, SystemRoles.Administrator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Administrator role cannot be assigned through invite.");

        var normalizedRole = roleName.ToUpperInvariant() switch
        {
            "MANAGER" => SystemRoles.Manager,
            "WAITRESS" => SystemRoles.Waitress,
            "CASHIER" => SystemRoles.Cashier,
            "STAFF" => SystemRoles.Waitress,
            _ => throw new InvalidOperationException("Role must be Manager, Waitress, or Cashier."),
        };

        var users = _unitOfWork.Repository<User>();
        var tenantUsers = _unitOfWork.Repository<TenantUser>();
        var rolesRepo = _unitOfWork.Repository<Role>();

        var normalizedEmail = dto.Email.Trim().ToUpperInvariant();
        var email = dto.Email.Trim();

        var roleEntity = await rolesRepo.Query().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == normalizedRole, cancellationToken)
            ?? throw new InvalidOperationException($"Role '{normalizedRole}' is not configured for this tenant.");

        var existingUser = await users.Query().FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser is null)
        {
            if (string.IsNullOrWhiteSpace(dto.Password))
                throw new InvalidOperationException("Password is required for new users.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                NormalizedEmail = normalizedEmail,
                PasswordHash = _passwordHasher.Hash(dto.Password!),
                DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? null : dto.DisplayName.Trim(),
            };

            var tenantUser = new TenantUser
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
            };

            tenantUser.TenantUserRoles.Add(new TenantUserRole
            {
                Id = Guid.NewGuid(),
                TenantUserId = tenantUser.Id,
                RoleId = roleEntity.Id,
            });

            await users.AddAsync(user, cancellationToken);
            await tenantUsers.AddAsync(tenantUser, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new InvitedTenantUserDto
            {
                UserId = user.Id,
                TenantUserId = tenantUser.Id,
                Email = user.Email,
                Roles = new[] { normalizedRole },
            };
        }

        var existingMembership = await tenantUsers.Query()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(tu => tu.UserId == existingUser.Id && tu.TenantId == tenantId, cancellationToken);

        if (existingMembership is not null)
            throw new InvalidOperationException("This user is already a member of this tenant.");

        var newTenantUser = new TenantUser
        {
            Id = Guid.NewGuid(),
            UserId = existingUser.Id,
        };

        newTenantUser.TenantUserRoles.Add(new TenantUserRole
        {
            Id = Guid.NewGuid(),
            TenantUserId = newTenantUser.Id,
            RoleId = roleEntity.Id,
        });

        await tenantUsers.AddAsync(newTenantUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new InvitedTenantUserDto
        {
            UserId = existingUser.Id,
            TenantUserId = newTenantUser.Id,
            Email = existingUser.Email,
            Roles = new[] { normalizedRole },
        };
    }
}
