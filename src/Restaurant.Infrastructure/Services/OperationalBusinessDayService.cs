using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class OperationalBusinessDayService : IOperationalBusinessDayService
{
    private const int MaxSkipClosedDays = 366;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;

    public OperationalBusinessDayService(ApplicationDbContext db, ICurrentTenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<EffectiveOperationalDay> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        var settings = await GetOrCreateTenantSettingsAsync(tenantId, cancellationToken);
        var cutoffHour = settings.OperationalDayCutoffHour;
        var utcNow = DateTime.UtcNow;
        var clockDate = BusinessDayCalculator.ResolveBusinessDate(utcNow, tenant.TimeZoneId, cutoffHour);

        if (OperationalBusinessDay.ShouldClearActiveDate(clockDate, settings.ActiveOperationalBusinessDate))
        {
            settings.ActiveOperationalBusinessDate = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var startDate = clockDate;
        if (settings.ActiveOperationalBusinessDate is { } active && active > startDate)
            startDate = active;

        var businessDate = await SkipClosedDaysAsync(tenantId, startDate, cancellationToken);

        if (businessDate > clockDate && settings.ActiveOperationalBusinessDate != businessDate)
        {
            settings.ActiveOperationalBusinessDate = businessDate;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var closure = await _db.DailyClosures.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.BusinessDate == businessDate, cancellationToken);

        return new EffectiveOperationalDay(
            businessDate,
            clockDate,
            businessDate > clockDate,
            cutoffHour,
            closure?.Status ?? DailyClosureStatus.Open);
    }

    private async Task<DateOnly> SkipClosedDaysAsync(
        Guid tenantId,
        DateOnly startDate,
        CancellationToken cancellationToken)
    {
        var endExclusive = startDate.AddDays(MaxSkipClosedDays);
        var closedDates = (await _db.DailyClosures.AsNoTracking()
            .Where(c =>
                c.TenantId == tenantId
                && c.BusinessDate >= startDate
                && c.BusinessDate < endExclusive
                && c.Status == DailyClosureStatus.Closed)
            .Select(c => c.BusinessDate)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var date = startDate;
        for (var i = 0; i < MaxSkipClosedDays; i++)
        {
            if (!closedDates.Contains(date))
                return date;

            date = date.AddDays(1);
        }

        return startDate;
    }

    private async Task<TenantSettings> GetOrCreateTenantSettingsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var settings = await _db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (settings is not null)
            return settings;

        settings = new TenantSettings
        {
            TenantId = tenantId,
            MaxDiscountPercent = 10m,
            OperationalDayCutoffHour = 4,
        };
        await _db.TenantSettings.AddAsync(settings, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private Guid ResolveTenantId()
    {
        if (_tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty)
            return tenantId;
        throw new InvalidOperationException("Tenant context is not available.");
    }
}
