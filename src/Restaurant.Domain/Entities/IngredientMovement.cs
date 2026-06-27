using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class IngredientMovement : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid IngredientMovementDocumentId { get; set; }
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? StockQuantitySnapshot { get; set; }
    public decimal? UnitCostSnapshot { get; set; }

    public IngredientMovementDocument Document { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
