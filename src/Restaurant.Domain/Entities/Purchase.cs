using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class Purchase : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProviderId { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public DateTime PurchasedAtUtc { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }

    public Provider Provider { get; set; } = null!;
    public ICollection<PurchaseLine> Lines { get; set; } = new List<PurchaseLine>();
}
