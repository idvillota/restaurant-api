using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class TenantUser : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<TenantUserRole> TenantUserRoles { get; set; } = new List<TenantUserRole>();
    public Employee? Employee { get; set; }
}
