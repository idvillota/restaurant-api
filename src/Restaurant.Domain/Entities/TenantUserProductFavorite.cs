using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class TenantUserProductFavorite : EntityBase, ITenantScoped
{
    public const int MaxPerMembership = 10;

    public Guid TenantId { get; set; }
    public Guid TenantUserId { get; set; }
    public Guid ProductId { get; set; }
    public int SortOrder { get; set; }

    public TenantUser TenantUser { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
