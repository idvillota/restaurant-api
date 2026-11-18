using Restaurant.Application.Common;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;

namespace Restaurant.Infrastructure.Common;

internal static class OperationalDayQueries
{
    public static IQueryable<BillLine> PaidBillLinesOnOperationalDays(
        IQueryable<BillLine> lines,
        DateOnly startDate,
        DateOnly endDate,
        DateTime startUtc,
        DateTime endExclusive) =>
        lines.Where(l =>
            l.Bill.Status == BillStatus.Paid
            && (
                (l.Bill.CashierShift != null
                    && l.Bill.CashierShift.BusinessDate >= startDate
                    && l.Bill.CashierShift.BusinessDate <= endDate)
                || (l.Bill.CashierShiftId == null
                    && l.Bill.PaidAtUtc != null
                    && l.Bill.PaidAtUtc >= startUtc
                    && l.Bill.PaidAtUtc < endExclusive)));

    public static IQueryable<Payment> CompletedPaymentsOnOperationalDays(
        IQueryable<Payment> payments,
        DateOnly startDate,
        DateOnly endDate,
        DateTime startUtc,
        DateTime endExclusive) =>
        payments.Where(p =>
            p.Status == PaymentStatus.Completed
            && (
                (p.CashierShift != null
                    && p.CashierShift.BusinessDate >= startDate
                    && p.CashierShift.BusinessDate <= endDate)
                || (p.CashierShiftId == null
                    && p.Bill != null
                    && p.Bill.CashierShift != null
                    && p.Bill.CashierShift.BusinessDate >= startDate
                    && p.Bill.CashierShift.BusinessDate <= endDate)
                || (p.CashierShiftId == null
                    && (p.Bill == null || p.Bill.CashierShiftId == null)
                    && p.PaidAtUtc >= startUtc
                    && p.PaidAtUtc < endExclusive)));

    public static DateOnly ResolveBusinessDate(
        DateOnly? paymentShiftDate,
        DateOnly? billShiftDate,
        DateTime paidAtUtc) =>
        paymentShiftDate ?? billShiftDate ?? ColombiaTime.CalendarDate(paidAtUtc);
}
