using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class Payment : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? BillId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ExternalReference { get; set; }
    public DateTime PaidAtUtc { get; set; }
    public Guid? CashierShiftId { get; set; }
    public Guid? ProcessedByUserId { get; set; }

    public Bill? Bill { get; set; }
    public Invoice? Invoice { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public CashierShift? CashierShift { get; set; }
}
