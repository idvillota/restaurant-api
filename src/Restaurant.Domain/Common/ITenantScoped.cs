namespace Restaurant.Domain.Common;

public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
