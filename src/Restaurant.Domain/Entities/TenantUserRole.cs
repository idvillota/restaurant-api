using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class TenantUserRole : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TenantUserId { get; set; }
    public Guid RoleId { get; set; }

    public TenantUser TenantUser { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
