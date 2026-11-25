using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

/// <summary>
/// Additive production batch for product-unit stock (Mode C). Each document increases product stock.
/// </summary>
public class ProductProductionDocument : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<ProductProductionLine> Lines { get; set; } = new List<ProductProductionLine>();
}
