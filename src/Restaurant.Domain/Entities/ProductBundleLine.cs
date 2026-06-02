using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class ProductBundleLine : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid ComponentProductId { get; set; }
    public decimal Quantity { get; set; }
    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;
    public Product ComponentProduct { get; set; } = null!;
}
