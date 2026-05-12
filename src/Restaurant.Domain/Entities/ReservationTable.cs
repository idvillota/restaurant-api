using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class ReservationTable : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid DiningTableId { get; set; }

    public Reservation Reservation { get; set; } = null!;
    public DiningTable DiningTable { get; set; } = null!;
}
