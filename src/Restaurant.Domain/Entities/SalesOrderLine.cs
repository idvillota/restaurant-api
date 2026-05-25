using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class SalesOrderLine : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }

    /// <summary>When set, this line was included on a kitchen ticket for preparation.</summary>
    public DateTime? SentToKitchenAtUtc { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ICollection<SalesOrderLineExcludedIngredient> ExcludedIngredients { get; set; } =
        new List<SalesOrderLineExcludedIngredient>();
}
