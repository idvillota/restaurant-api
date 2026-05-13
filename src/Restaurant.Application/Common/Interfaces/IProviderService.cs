using Restaurant.Application.Features.Procurement.Providers;

namespace Restaurant.Application.Common.Interfaces;

public interface IProviderService
{
    Task<IReadOnlyList<ProviderDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ProviderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProviderDto> CreateAsync(CreateProviderDto dto, CancellationToken cancellationToken = default);
    Task<ProviderDto?> UpdateAsync(Guid id, UpdateProviderDto dto, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
