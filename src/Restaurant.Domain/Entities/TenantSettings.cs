using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class TenantSettings : ITenantScoped
{
    public Guid TenantId { get; set; }
    public decimal MaxDiscountPercent { get; set; } = 10m;
    /// <summary>Hour (0-23) in tenant local time when the operational day rolls over (e.g. 4 = sales until 03:59 belong to previous day).</summary>
    public int OperationalDayCutoffHour { get; set; } = 4;

    /// <summary>When set and ahead of the clock-based operational date, operations use this date (e.g. after daily closure advances to the next day).</summary>
    public DateOnly? ActiveOperationalBusinessDate { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
