using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class Ingredient : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public IngredientUnit Unit { get; set; }
    public decimal? StockQuantity { get; set; }
    public decimal? ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProductIngredient> ProductIngredients { get; set; } = new List<ProductIngredient>();
}
