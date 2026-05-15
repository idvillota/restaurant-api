using Restaurant.Application.Features.Organization.TenantUsers;

namespace Restaurant.Application.Common.Interfaces;

public interface ITenantUserInviteService
{
    Task<InvitedTenantUserDto> InviteAsync(InviteTenantUserDto dto, CancellationToken cancellationToken = default);
}
