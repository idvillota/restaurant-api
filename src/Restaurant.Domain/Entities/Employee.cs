using Restaurant.Domain.Common;

namespace Restaurant.Domain.Entities;

public class Employee : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? TenantUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public DateOnly? HiredOn { get; set; }
    public bool IsActive { get; set; } = true;

    public TenantUser? TenantUser { get; set; }
}
