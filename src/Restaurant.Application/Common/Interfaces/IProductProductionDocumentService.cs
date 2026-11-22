using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Inventory.ProductProductions;

namespace Restaurant.Application.Common.Interfaces;

public interface IProductProductionDocumentService
{
    Task<PagedResult<ProductProductionDocumentListItemDto>> ListAsync(
        ListQuery query,
        CancellationToken cancellationToken = default);

    Task<ProductProductionDocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Next suggested number for the create dialog (PROD-yyyyMMdd-####).</summary>
    Task<string> SuggestNextDocumentNumberAsync(CancellationToken cancellationToken = default);

    Task<ProductProductionDocumentDto> CreateAsync(
        CreateProductProductionDocumentDto dto,
        CancellationToken cancellationToken = default);
}
