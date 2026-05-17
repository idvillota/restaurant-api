using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Operations.DiningTables;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Common.Interfaces;

public interface IDiningTableService
{
    Task<PagedResult<DiningTableDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default);
    Task<DiningTableDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DiningTableDto> CreateAsync(CreateDiningTableDto dto, CancellationToken cancellationToken = default);
    Task<DiningTableDto?> UpdateAsync(Guid id, UpdateDiningTableDto dto, CancellationToken cancellationToken = default);
    Task<DiningTableDto?> SetStatusAsync(Guid id, ETableStatus status, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
