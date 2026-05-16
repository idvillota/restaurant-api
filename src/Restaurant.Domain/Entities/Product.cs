using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class Product : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Sku { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; } = true;

    public ProductType ProductType { get; set; } = null!;
    public ICollection<ProductIngredient> ProductIngredients { get; set; } = new List<ProductIngredient>();
    public ICollection<SalesOrderLine> SalesOrderLines { get; set; } = new List<SalesOrderLine>();
}
