using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class Role : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<TenantUserRole> TenantUserRoles { get; set; } = new List<TenantUserRole>();
    public ICollection<RoleFeature> RoleFeatures { get; set; } = new List<RoleFeature>();
}
