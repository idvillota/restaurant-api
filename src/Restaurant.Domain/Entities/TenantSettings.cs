using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class TenantSettings : ITenantScoped
{
    public Guid TenantId { get; set; }
    public decimal MaxDiscountPercent { get; set; } = 10m;

    public Tenant Tenant { get; set; } = null!;
}
