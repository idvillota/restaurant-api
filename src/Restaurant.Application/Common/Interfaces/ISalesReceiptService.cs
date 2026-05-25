using Restaurant.Application.Features.Sales.SalesReceipts;
using Restaurant.Domain.Entities;

namespace Restaurant.Application.Common.Interfaces;

public interface ISalesReceiptService
{
    Task<SalesReceiptModel> BuildModelAsync(Bill bill, TenantSettings settings, CancellationToken cancellationToken = default);

    Task<SalesReceiptFilesDto> GenerateFilesAsync(
        SalesReceiptModel model,
        CancellationToken cancellationToken = default);
}
