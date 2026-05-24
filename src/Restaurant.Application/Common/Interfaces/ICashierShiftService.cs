using Restaurant.Application.Features.Cashier;

namespace Restaurant.Application.Common.Interfaces;

public interface ICashierShiftService
{
    Task<BusinessDayContextDto> GetBusinessDayContextAsync(CancellationToken cancellationToken = default);
    Task<CashierShiftSummaryDto?> GetMyOpenShiftAsync(CancellationToken cancellationToken = default);
    Task<CashierShiftSummaryDto> OpenShiftAsync(OpenCashierShiftDto dto, CancellationToken cancellationToken = default);
    Task<CashierShiftReportDto> CloseShiftAsync(Guid shiftId, CloseCashierShiftDto dto, CancellationToken cancellationToken = default);
    Task<CashierShiftReportDto> GetShiftReportAsync(Guid shiftId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashierShiftSummaryDto>> ListShiftsAsync(DateOnly? businessDate, CancellationToken cancellationToken = default);
    Task<CashMovementDto> RecordCashMovementAsync(CreateCashMovementDto dto, CancellationToken cancellationToken = default);
    Task<(Guid ShiftId, Guid UserId)> RequireOpenShiftAsync(CancellationToken cancellationToken = default);
}

public interface IDailyClosureService
{
    Task<DailyClosureReportDto> GetDailyReportAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<DailyClosureReportDto> CloseDailyAsync(DateOnly businessDate, CloseDailyClosureDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyClosureSummaryDto>> ListClosuresAsync(CancellationToken cancellationToken = default);
}
