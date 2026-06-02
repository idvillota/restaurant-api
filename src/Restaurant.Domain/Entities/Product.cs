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
    public string? Sku { get; set; }
    public string? ImagePath { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; } = true;

    public ProductType ProductType { get; set; } = null!;
    public ICollection<ProductIngredient> ProductIngredients { get; set; } = new List<ProductIngredient>();
    public ICollection<ProductBundleLine> BundleComponents { get; set; } = new List<ProductBundleLine>();
    public ICollection<SalesOrderLine> SalesOrderLines { get; set; } = new List<SalesOrderLine>();
}
