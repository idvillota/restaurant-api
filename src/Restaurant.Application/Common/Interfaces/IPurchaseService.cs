using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Procurement.Purchases;

namespace Restaurant.Application.Common.Interfaces;

public interface IPurchaseService
{
    Task<PagedResult<PurchaseListItemDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default);
    Task<PurchaseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseDto> CreateAsync(CreatePurchaseDto dto, CancellationToken cancellationToken = default);
    Task<PurchaseDto?> UpdatePaymentDateAsync(
        Guid id,
        UpdatePurchasePaymentDateDto dto,
        CancellationToken cancellationToken = default);
}
