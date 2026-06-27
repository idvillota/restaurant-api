using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Inventory.IngredientMovementTypes;

namespace Restaurant.Application.Common.Interfaces;

public interface IIngredientMovementTypeService
{
    Task<PagedResult<IngredientMovementTypeDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default);
    Task<IngredientMovementTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IngredientMovementTypeDto> CreateAsync(CreateIngredientMovementTypeDto dto, CancellationToken cancellationToken = default);
    Task<IngredientMovementTypeDto?> UpdateAsync(Guid id, UpdateIngredientMovementTypeDto dto, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
