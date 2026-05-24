using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class Bill : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public string Number { get; set; } = string.Empty;
    public BillStatus Status { get; set; } = BillStatus.Issued;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal TipAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public Guid? CashierShiftId { get; set; }
    public Guid? ProcessedByUserId { get; set; }

    public Customer Customer { get; set; } = null!;
    public CashierShift? CashierShift { get; set; }
    public ICollection<BillSalesOrder> BillOrders { get; set; } = new List<BillSalesOrder>();
    public Invoice? Invoice { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
