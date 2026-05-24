using System.ComponentModel.DataAnnotations;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Cashier;

public sealed class OpenCashierShiftDto
{
    [Range(0, double.MaxValue)]
    public decimal OpeningFloat { get; set; }
}

public sealed class CloseCashierShiftDto
{
    [Range(0, double.MaxValue)]
    public decimal CountedCash { get; set; }

    [MaxLength(500)]
    public string? ClosingNotes { get; set; }
}

public sealed class CashierShiftSummaryDto
{
    public Guid Id { get; set; }
    public Guid CashierUserId { get; set; }
    public string CashierEmail { get; set; } = string.Empty;
    public CashierShiftStatus Status { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? CashOverShort { get; set; }
}

public sealed class ShiftPaymentLineDto
{
    public Guid PaymentId { get; set; }
    public Guid BillId { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime PaidAtUtc { get; set; }
}

public sealed class ShiftTotalsByMethodDto
{
    public PaymentMethod Method { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public sealed class CashMovementDto
{
    public Guid Id { get; set; }
    public CashMovementType MovementType { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod? Method { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? PurchaseId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public sealed class CashierShiftReportDto
{
    public CashierShiftSummaryDto Shift { get; set; } = null!;
    public decimal TotalSales { get; set; }
    public int BillCount { get; set; }
    public IReadOnlyList<ShiftTotalsByMethodDto> TotalsByMethod { get; set; } = [];
    public IReadOnlyList<ShiftPaymentLineDto> Payments { get; set; } = [];
    public IReadOnlyList<CashMovementDto> CashOutflows { get; set; } = [];
    public decimal CashOutflowTotal { get; set; }
}

public sealed class CreateCashMovementDto
{
  public CashMovementType MovementType { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public PaymentMethod? Method { get; set; }

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public Guid? PurchaseId { get; set; }

    public Guid? CashierShiftId { get; set; }
}

public sealed class DailyClosureSummaryDto
{
    public DateOnly BusinessDate { get; set; }
    public DailyClosureStatus Status { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedByEmail { get; set; }
}

public sealed class DailyShiftRollupDto
{
    public Guid ShiftId { get; set; }
    public string CashierEmail { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public CashierShiftStatus Status { get; set; }
    public decimal TotalSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? CashOverShort { get; set; }
}

public sealed class DailyClosureReportDto
{
    /// <summary>Set when the day was just closed; use to open shifts for late sales on the next operational day.</summary>
    public DateOnly? NextOperationalBusinessDate { get; set; }

    public DailyClosureSummaryDto Closure { get; set; } = null!;
    public decimal TotalSales { get; set; }
    public int BillCount { get; set; }
    public decimal TotalCashIn { get; set; }
    public decimal TotalCard { get; set; }
    public decimal TotalTransfer { get; set; }
    public decimal TotalOther { get; set; }
    public decimal TotalCashOutflows { get; set; }
    public decimal NetCash { get; set; }
    public IReadOnlyList<DailyShiftRollupDto> Shifts { get; set; } = [];
    public IReadOnlyList<CashMovementDto> CashOutflows { get; set; } = [];
    public IReadOnlyList<ShiftPaymentLineDto> Payments { get; set; } = [];
}

public sealed class CloseDailyClosureDto
{
    [MaxLength(500)]
    public string? Notes { get; set; }
}

public sealed class BusinessDayContextDto
{
    public DateOnly BusinessDate { get; set; }
    /// <summary>Operational date from clock and cutoff only (before manual advance).</summary>
    public DateOnly ClockBusinessDate { get; set; }
    public bool IsAdvancedBeyondClock { get; set; }
    public int OperationalDayCutoffHour { get; set; }
    public DailyClosureStatus DailyClosureStatus { get; set; }
    public int OpenShiftsCount { get; set; }
    public CashierShiftSummaryDto? MyOpenShift { get; set; }
}
