using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class Reservation : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? CustomerId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public int PartySize { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public string? Notes { get; set; }

    public Customer? Customer { get; set; }
    public ICollection<ReservationTable> ReservationTables { get; set; } = new List<ReservationTable>();
}
