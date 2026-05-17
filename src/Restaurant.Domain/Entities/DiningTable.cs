using Restaurant.Domain.Common;
using Restaurant.Domain.Enums;

namespace Restaurant.Domain.Entities;

public class DiningTable : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Zone { get; set; }
    public ETableStatus Status { get; set; } = ETableStatus.Available;
    public bool IsActive { get; set; } = true;

    public ICollection<ReservationTable> ReservationTables { get; set; } = new List<ReservationTable>();
}
