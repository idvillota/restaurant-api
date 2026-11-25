using Restaurant.Application.Features.Auth;

namespace Restaurant.Application.Common.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterTenantAsync(RegisterTenantDto dto, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);

    /// <summary>Platform admin only: list active tenant memberships for the current user.</summary>
    Task<IReadOnlyList<LoginTenantOptionDto>> ListMyTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>Platform admin only: issue a new token for another membership (no password).</summary>
    Task<AuthResponseDto> SwitchTenantAsync(SwitchTenantDto dto, CancellationToken cancellationToken = default);
}
