using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class SalesOrderLineExcludedIngredient : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SalesOrderLineId { get; set; }
    public Guid IngredientId { get; set; }

    public SalesOrderLine SalesOrderLine { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
