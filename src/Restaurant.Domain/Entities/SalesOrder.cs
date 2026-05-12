using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class SalesOrder : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? CustomerId { get; set; }
    public string Number { get; set; } = string.Empty;
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public Customer? Customer { get; set; }
    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
