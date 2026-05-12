using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class Customer : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? TaxId { get; set; }
}
