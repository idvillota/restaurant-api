using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Organization.TenantUsers;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
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
        EnsureTenant();

        var tenantId = _tenantContext.TenantId!.Value;
        var tenants = _unitOfWork.Repository<Tenant>();
        var tenant = await tenants.Query().AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        if (!tenant.IsActive)
            throw new InvalidOperationException("Tenant is not active.");

        var normalizedRole = NormalizeAssignableRole(dto.Role);

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
            .Include(tu => tu.User)
            .FirstOrDefaultAsync(tu => tu.UserId == existingUser.Id && tu.TenantId == tenantId, cancellationToken);

        if (existingMembership is not null)
        {
            if (existingMembership.IsActive)
                throw new InvalidOperationException("This user is already a member of this tenant.");

            return await ReactivateMembershipAsync(
                existingMembership,
                roleEntity.Id,
                normalizedRole,
                dto.DisplayName,
                cancellationToken);
        }

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

    public async Task<IReadOnlyList<TenantUserMemberDto>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();

        var query = _unitOfWork.Repository<TenantUser>().Query()
            .AsNoTracking()
            .Include(tu => tu.User)
            .Include(tu => tu.TenantUserRoles).ThenInclude(tur => tur.Role)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(tu => tu.IsActive);

        var members = await query
            .OrderBy(tu => tu.User.DisplayName)
            .ThenBy(tu => tu.User.Email)
            .ToListAsync(cancellationToken);

        return members.Select(m => ToMemberDto(m)).ToList();
    }

    public async Task<TenantUserMemberDto> ChangeRoleAsync(
        Guid tenantUserId,
        ChangeTenantUserRoleDto dto,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();

        var actorUserId = _tenantContext.UserId;
        if (actorUserId is null || actorUserId == Guid.Empty)
            throw new InvalidOperationException("Current user is not available.");

        var normalizedRole = NormalizeAssignableRole(dto.Role);
        var tenantUsers = _unitOfWork.Repository<TenantUser>();
        var rolesRepo = _unitOfWork.Repository<Role>();

        var membership = await tenantUsers.Query()
            .Include(tu => tu.User)
            .FirstOrDefaultAsync(tu => tu.Id == tenantUserId && tu.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("User was not found in this tenant.");

        if (membership.UserId == actorUserId.Value)
            throw new InvalidOperationException("You cannot change your own role.");

        await EnsureCanManageAdministratorAsync(membership.Id, actorUserId.Value, "change", cancellationToken);

        var roleEntity = await rolesRepo.Query().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == normalizedRole, cancellationToken)
            ?? throw new InvalidOperationException($"Role '{normalizedRole}' is not configured for this tenant.");

        await ReplaceRolesAsync(membership.Id, roleEntity.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToMemberDto(membership, normalizedRole);
    }

    public async Task<TenantUserMemberDto> SetActiveAsync(
        Guid tenantUserId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();

        var actorUserId = _tenantContext.UserId;
        if (actorUserId is null || actorUserId == Guid.Empty)
            throw new InvalidOperationException("Current user is not available.");

        var tenantUsers = _unitOfWork.Repository<TenantUser>();
        var membership = await tenantUsers.Query()
            .Include(tu => tu.User)
            .Include(tu => tu.TenantUserRoles).ThenInclude(tur => tur.Role)
            .FirstOrDefaultAsync(tu => tu.Id == tenantUserId, cancellationToken)
            ?? throw new InvalidOperationException("User was not found in this tenant.");

        if (membership.UserId == actorUserId.Value)
            throw new InvalidOperationException("You cannot change your own access.");

        if (membership.IsActive == isActive)
            return ToMemberDto(membership);

        if (!isActive)
        {
            await EnsureCanManageAdministratorAsync(membership.Id, actorUserId.Value, "disable", cancellationToken);

            var hasOpenShift = await _unitOfWork.Repository<CashierShift>().Query()
                .AnyAsync(
                    s => s.CashierUserId == membership.UserId && s.Status == CashierShiftStatus.Open,
                    cancellationToken);
            if (hasOpenShift)
                throw new InvalidOperationException("Close the open cashier shift before disabling access.");

            membership.IsActive = false;
        }
        else
        {
            membership.IsActive = true;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToMemberDto(membership);
    }

    private async Task<InvitedTenantUserDto> ReactivateMembershipAsync(
        TenantUser membership,
        Guid roleId,
        string normalizedRole,
        string? displayName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            membership.User.DisplayName = displayName.Trim();

        membership.IsActive = true;
        await ReplaceRolesAsync(membership.Id, roleId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new InvitedTenantUserDto
        {
            UserId = membership.UserId,
            TenantUserId = membership.Id,
            Email = membership.User.Email,
            Roles = new[] { normalizedRole },
        };
    }

    private async Task ReplaceRolesAsync(Guid tenantUserId, Guid roleId, CancellationToken cancellationToken)
    {
        var tenantUserRoles = _unitOfWork.Repository<TenantUserRole>();
        var existingRoleRows = await tenantUserRoles.Query()
            .IgnoreQueryFilters()
            .Where(tur => tur.TenantUserId == tenantUserId)
            .ToListAsync(cancellationToken);
        foreach (var existing in existingRoleRows)
            tenantUserRoles.Remove(existing);

        await tenantUserRoles.AddAsync(new TenantUserRole
        {
            Id = Guid.NewGuid(),
            TenantUserId = tenantUserId,
            RoleId = roleId,
        }, cancellationToken);
    }

    private async Task EnsureCanManageAdministratorAsync(
        Guid tenantUserId,
        Guid actorUserId,
        string action,
        CancellationToken cancellationToken)
    {
        var currentRoleNames = await _unitOfWork.Repository<TenantUserRole>().Query()
            .AsNoTracking()
            .Where(tur => tur.TenantUserId == tenantUserId)
            .Select(tur => tur.Role.Name)
            .ToListAsync(cancellationToken);

        if (!SystemRoles.IsGeneralAdministrator(currentRoleNames))
            return;

        var actorIsGeneralAdmin = await ActorIsGeneralAdministratorAsync(actorUserId, cancellationToken);
        if (!actorIsGeneralAdmin)
            throw new InvalidOperationException($"Only an Administrator can {action} another Administrator.");

        var generalAdminCount = await CountGeneralAdministratorsAsync(cancellationToken);
        if (generalAdminCount <= 1)
            throw new InvalidOperationException("Cannot change the last Administrator.");
    }

    private void EnsureTenant()
    {
        if (!_tenantContext.TenantId.HasValue)
            throw new InvalidOperationException("Tenant context is not set.");
    }

    private async Task<bool> ActorIsGeneralAdministratorAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        var names = await _unitOfWork.Repository<TenantUser>().Query()
            .AsNoTracking()
            .Where(tu => tu.UserId == actorUserId && tu.IsActive)
            .SelectMany(tu => tu.TenantUserRoles.Select(tur => tur.Role.Name))
            .ToListAsync(cancellationToken);
        return SystemRoles.IsGeneralAdministrator(names);
    }

    private async Task<int> CountGeneralAdministratorsAsync(CancellationToken cancellationToken)
    {
        var namesByMember = await _unitOfWork.Repository<TenantUser>().Query()
            .AsNoTracking()
            .Where(tu => tu.IsActive)
            .Select(tu => new
            {
                tu.Id,
                RoleNames = tu.TenantUserRoles.Select(tur => tur.Role.Name).ToList(),
            })
            .ToListAsync(cancellationToken);

        return namesByMember.Count(m => SystemRoles.IsGeneralAdministrator(m.RoleNames));
    }

    private static string NormalizeAssignableRole(string roleName)
    {
        var trimmed = roleName.Trim();
        if (string.Equals(trimmed, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, SystemRoles.Administrator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Administrator role cannot be assigned through this action.");

        return trimmed.ToUpperInvariant() switch
        {
            "MANAGER" => SystemRoles.Manager,
            "WAITRESS" => SystemRoles.Waitress,
            "CASHIER" => SystemRoles.Cashier,
            "STAFF" => SystemRoles.Waitress,
            _ => throw new InvalidOperationException("Role must be Manager, Waitress, or Cashier."),
        };
    }

    private static TenantUserMemberDto ToMemberDto(TenantUser membership, string? roleOverride = null)
    {
        return new TenantUserMemberDto
        {
            UserId = membership.UserId,
            TenantUserId = membership.Id,
            Email = membership.User.Email,
            DisplayName = membership.User.DisplayName,
            Role = roleOverride ?? SelectPrimaryRole(membership.TenantUserRoles.Select(tur => tur.Role.Name)),
            IsActive = membership.IsActive,
        };
    }

    /// <summary>
    /// Product rule: one role per user. If legacy rows have more than one, surface a single primary name.
    /// </summary>
    private static string SelectPrimaryRole(IEnumerable<string> roleNames)
    {
        var names = roleNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string[] priority =
        [
            SystemRoles.Administrator,
            SystemRoles.Owner,
            SystemRoles.Manager,
            SystemRoles.Cashier,
            SystemRoles.Waitress,
            SystemRoles.Staff,
        ];

        foreach (var candidate in priority)
        {
            var match = names.FirstOrDefault(n => string.Equals(n, candidate, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                continue;
            if (string.Equals(match, SystemRoles.Staff, StringComparison.OrdinalIgnoreCase))
                return SystemRoles.Waitress;
            if (string.Equals(match, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase))
                return SystemRoles.Administrator;
            return match;
        }

        return names.FirstOrDefault() ?? string.Empty;
    }
}
