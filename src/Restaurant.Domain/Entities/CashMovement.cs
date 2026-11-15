using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class CashMovement : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? CashierShiftId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public CashMovementType MovementType { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod? Method { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? PurchaseId { get; set; }
    public Guid? ProviderId { get; set; }

    /// <summary>
    /// When true (default), cash left the drawer and the amount counts in shift/day cash totals.
    /// When false, the movement is listed on reports as informational only.
    /// </summary>
    public bool IsCashDrawerPayment { get; set; } = true;

    public Guid CreatedByUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }

    public bool IsVoided => VoidedAtUtc is not null;

    public CashierShift? CashierShift { get; set; }
    public Purchase? Purchase { get; set; }
    public Provider? Provider { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public User? VoidedByUser { get; set; }
}
