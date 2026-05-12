namespace Restaurant.Application.Common.Interfaces;

public interface ICurrentTenantContext
{
    Guid? TenantId { get; set; }
    Guid? UserId { get; set; }
}
