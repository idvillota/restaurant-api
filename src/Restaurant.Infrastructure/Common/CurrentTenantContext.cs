using Restaurant.Application.Common.Interfaces;

namespace Restaurant.Infrastructure.Common;

public sealed class CurrentTenantContext : ICurrentTenantContext
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
}
