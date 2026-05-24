using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class DailyClosure : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DailyClosureStatus Status { get; set; } = DailyClosureStatus.Open;
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? Notes { get; set; }

    public User? ClosedByUser { get; set; }
}
