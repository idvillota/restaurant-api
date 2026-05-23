using Restaurant.Application.Features.Sales.Bills;

namespace Restaurant.Application.Common.Interfaces;

public interface ITenantSettingsService
{
    Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<TenantSettingsDto> UpdateAsync(UpdateTenantSettingsDto dto, CancellationToken cancellationToken = default);
}
