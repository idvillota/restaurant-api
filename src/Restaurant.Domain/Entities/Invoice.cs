using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class Invoice : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? CustomerId { get; set; }
    public string Number { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? DueAtUtc { get; set; }

    public SalesOrder? SalesOrder { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
