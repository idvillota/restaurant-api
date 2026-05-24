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
    public Guid CreatedByUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public CashierShift? CashierShift { get; set; }
    public Purchase? Purchase { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
