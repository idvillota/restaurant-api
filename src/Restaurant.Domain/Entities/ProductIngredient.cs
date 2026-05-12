using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class ProductIngredient : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }

    public Product Product { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
