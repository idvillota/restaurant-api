using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class CashierShift : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CashierUserId { get; set; }
    public CashierShiftStatus Status { get; set; } = CashierShiftStatus.Open;
    public DateOnly BusinessDate { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public string? ClosingNotes { get; set; }

    public User CashierUser { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<CashMovement> CashMovements { get; set; } = new List<CashMovement>();
}
