using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class Tenant : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? TimeZoneId { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public bool IsActive { get; set; } = true;

    public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
}
