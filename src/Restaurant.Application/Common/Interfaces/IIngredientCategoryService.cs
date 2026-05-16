using Restaurant.Application.Features.Catalog.IngredientCategories;

namespace Restaurant.Application.Common.Interfaces;

public interface IIngredientCategoryService
{
    Task<IReadOnlyList<IngredientCategoryDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IngredientCategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IngredientCategoryDto> CreateAsync(CreateIngredientCategoryDto dto, CancellationToken cancellationToken = default);
    Task<IngredientCategoryDto?> UpdateAsync(Guid id, UpdateIngredientCategoryDto dto, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
