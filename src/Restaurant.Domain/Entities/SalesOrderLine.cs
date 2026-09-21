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
    /// <summary>Recipe-based unit cost captured when the order line is paid.</summary>
    public decimal? UnitCostPrice { get; set; }
    public string? Notes { get; set; }

    /// <summary>When set, this line was included on a kitchen ticket for preparation.</summary>
    public DateTime? SentToKitchenAtUtc { get; set; }

    /// <summary>Cumulative quantity logically cancelled (never physically deleted).</summary>
    public decimal CancelledQuantity { get; set; }

    /// <summary>Set when the remaining active quantity reaches zero after a cancel.</summary>
    public DateTime? CancelledAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }

    /// <summary>Last cancel reason code or free-text detail (for audit / kitchen ticket).</summary>
    public string? CancelReason { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ICollection<SalesOrderLineExcludedIngredient> ExcludedIngredients { get; set; } =
        new List<SalesOrderLineExcludedIngredient>();
}
