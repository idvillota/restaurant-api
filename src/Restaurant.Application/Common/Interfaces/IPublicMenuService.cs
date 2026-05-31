using Restaurant.Application.Features.PublicMenu;

namespace Restaurant.Application.Common.Interfaces;

public interface IPublicMenuService
{
    Task<PublicMenuDto?> GetByTenantSlugAsync(string tenantSlug, CancellationToken cancellationToken = default);
}
