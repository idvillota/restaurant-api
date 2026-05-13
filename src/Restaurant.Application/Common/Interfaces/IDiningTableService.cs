using Restaurant.Application.Features.Operations.DiningTables;

namespace Restaurant.Application.Common.Interfaces;

public interface IDiningTableService
{
    Task<IReadOnlyList<DiningTableDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<DiningTableDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DiningTableDto> CreateAsync(CreateDiningTableDto dto, CancellationToken cancellationToken = default);
    Task<DiningTableDto?> UpdateAsync(Guid id, UpdateDiningTableDto dto, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
