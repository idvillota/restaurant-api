using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Procurement.Providers;

namespace Restaurant.Application.Common.Interfaces;

public interface IProviderService
{
    Task<PagedResult<ProviderDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default);
    Task<ProviderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProviderDto> CreateAsync(CreateProviderDto dto, CancellationToken cancellationToken = default);
    Task<ProviderDto?> UpdateAsync(Guid id, UpdateProviderDto dto, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
