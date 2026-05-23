namespace Restaurant.Application.Features.Organization.RolePermissions;

public sealed class FeatureDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class RolePermissionRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> FeatureCodes { get; set; } = [];
}

public sealed class RolePermissionMatrixDto
{
    public IReadOnlyList<FeatureDto> Features { get; set; } = [];
    public IReadOnlyList<RolePermissionRoleDto> Roles { get; set; } = [];
}

public sealed class UpdateRolePermissionsDto
{
    public IReadOnlyList<string> FeatureCodes { get; set; } = [];
}
