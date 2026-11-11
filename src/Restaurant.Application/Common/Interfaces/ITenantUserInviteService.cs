using Restaurant.Application.Features.Organization.TenantUsers;

namespace Restaurant.Application.Common.Interfaces;

public interface ITenantUserInviteService
{
    Task<InvitedTenantUserDto> InviteAsync(InviteTenantUserDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantUserMemberDto>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<TenantUserMemberDto> ChangeRoleAsync(
        Guid tenantUserId,
        ChangeTenantUserRoleDto dto,
        CancellationToken cancellationToken = default);

    Task<TenantUserMemberDto> SetActiveAsync(
        Guid tenantUserId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<TenantUserMemberDto> UpdateProfileAsync(
        Guid tenantUserId,
        UpdateTenantUserProfileDto dto,
        CancellationToken cancellationToken = default);
}
