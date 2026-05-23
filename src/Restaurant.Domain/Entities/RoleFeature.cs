using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class RoleFeature : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public Guid FeatureId { get; set; }

    public Role Role { get; set; } = null!;
    public Feature Feature { get; set; } = null!;
}
