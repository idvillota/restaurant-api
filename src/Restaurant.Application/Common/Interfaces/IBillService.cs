using Restaurant.Application.Features.Sales.Bills;

namespace Restaurant.Application.Common.Interfaces;

public interface IBillService
{
    Task<IReadOnlyList<PayableTableGroupDto>> ListPayableByTableSearchAsync(
        string? tableSearch,
        CancellationToken cancellationToken = default);

    Task<CheckoutTotalsDto> PreviewCheckoutAsync(CheckoutPreviewDto dto, CancellationToken cancellationToken = default);

    Task<BillDto> FinalizeCheckoutAsync(FinalizeCheckoutDto dto, CancellationToken cancellationToken = default);
}
