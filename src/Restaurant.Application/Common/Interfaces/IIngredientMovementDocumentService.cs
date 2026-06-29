using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Inventory.IngredientMovements;

namespace Restaurant.Application.Common.Interfaces;

public interface IIngredientMovementDocumentService
{
    Task<PagedResult<IngredientMovementDocumentListItemDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default);
    Task<IngredientMovementDocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IngredientMovementDocumentDto> CreateAsync(CreateIngredientMovementDocumentDto dto, CancellationToken cancellationToken = default);
}
