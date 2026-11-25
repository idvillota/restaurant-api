using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class ProductProductionLine : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductProductionDocumentId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Product stock before this line was applied.</summary>
    public decimal StockQuantitySnapshot { get; set; }

    public ProductProductionDocument Document { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
