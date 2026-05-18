using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class PurchaseLine : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public Purchase Purchase { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
