using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class Product : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductTypeId { get; set; }
    public EProductType CompositionType { get; set; } = EProductType.Prepared;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public decimal UnitPrice { get; set; }
    /// <summary>Used as product cost when tenant inventory is off. Ignored for COGS while inventory is on.</summary>
    public decimal ManualCostPrice { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When set (e.g. 10 or 20), selling this product creates a prepaid lunch-ticket booklet.
    /// Null means the product is not a tiquetera pack.
    /// </summary>
    public int? LunchTicketPackSize { get; set; }

    /// <summary>
    /// When true, this product may be paid only with payment method Tiquetera (typically Almuerzo).
    /// </summary>
    public bool IsLunchTicketRedeemable { get; set; }

    public ProductType ProductType { get; set; } = null!;
    public ICollection<ProductIngredient> ProductIngredients { get; set; } = new List<ProductIngredient>();
    public ICollection<ProductBundleLine> BundleComponents { get; set; } = new List<ProductBundleLine>();
    public ICollection<SalesOrderLine> SalesOrderLines { get; set; } = new List<SalesOrderLine>();
}
