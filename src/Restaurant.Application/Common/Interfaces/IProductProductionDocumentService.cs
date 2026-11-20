using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Inventory.ProductProductions;

namespace Restaurant.Application.Common.Interfaces;

public interface IProductProductionDocumentService
{
    Task<PagedResult<ProductProductionDocumentListItemDto>> ListAsync(
        ListQuery query,
        CancellationToken cancellationToken = default);

    Task<ProductProductionDocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductProductionDocumentDto> CreateAsync(
        CreateProductProductionDocumentDto dto,
        CancellationToken cancellationToken = default);
}
