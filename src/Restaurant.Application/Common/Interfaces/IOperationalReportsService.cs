using Restaurant.Application.Features.Reports;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Common.Interfaces;

public interface IOperationalReportsService
{
    Task<SalesReportDto> GetSalesReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        Guid? productId,
        CancellationToken cancellationToken = default);

    Task<IngredientsReportDto> GetIngredientsReportAsync(
        string? nameFilter,
        CancellationToken cancellationToken = default);

    Task<PurchasesReportDto> GetPurchasesReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        Guid? ingredientId,
        Guid? providerId,
        CancellationToken cancellationToken = default);

    Task<CashOutflowsReportDto> GetCashOutflowsReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        CashMovementType? movementType,
        Guid? providerId,
        bool? isCashDrawerPayment,
        CancellationToken cancellationToken = default);

    Task<DailySummaryReportDto> GetDailySummaryReportAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}
