using Restaurant.Application.Features.Catalog.Ingredients;

namespace Restaurant.Application.Common.Interfaces;

public interface IIngredientService
{
    Task<IReadOnlyList<IngredientDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<IngredientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IngredientDto> CreateAsync(CreateIngredientDto dto, CancellationToken cancellationToken = default);

    Task<IngredientDto?> UpdateAsync(Guid id, UpdateIngredientDto dto, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
