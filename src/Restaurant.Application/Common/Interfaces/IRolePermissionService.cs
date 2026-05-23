using Restaurant.Application.Features.Organization.RolePermissions;

namespace Restaurant.Application.Common.Interfaces;

public interface IRolePermissionService
{
    Task<RolePermissionMatrixDto> GetMatrixAsync(CancellationToken cancellationToken = default);

    Task UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsDto dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPermissionCodesForUserAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
