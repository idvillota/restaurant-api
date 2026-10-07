using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class PrintJobQueue : IPrintJobQueue
{
    private const int PendingLimit = 20;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IMobileSalePublisher _prints;

    public PrintJobQueue(
        ApplicationDbContext db,
        ICurrentTenantContext tenantContext,
        IMobileSalePublisher prints)
    {
        _db = db;
        _tenantContext = tenantContext;
        _prints = prints;
    }

    public async Task EnqueueAsync(
        string kind,
        string payloadFormat,
        string payload,
        CancellationToken cancellationToken = default,
        string? exceptDeviceId = null)
    {
        if (string.IsNullOrWhiteSpace(payload) || _tenantContext.TenantId is not Guid tenantId)
            return;

        var job = new PrintJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kind = kind,
            PayloadFormat = payloadFormat,
            Payload = payload,
            Status = PrintJob.StatusPending,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.PrintJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _prints.PublishPrintJobAsync(
                tenantId,
                job.Id,
                job.Kind,
                job.PayloadFormat,
                job.Payload,
                exceptDeviceId,
                cancellationToken);
        }
        catch (Exception)
        {
            // The job is already stored. A missed push must not undo the comanda or the bill.
        }
    }

    public async Task<IReadOnlyList<PrintJobTicket>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _db.PrintJobs.AsNoTracking()
            .Where(job => job.Status == PrintJob.StatusPending)
            .OrderBy(job => job.CreatedAtUtc)
            .Take(PendingLimit)
            .Select(job => new PrintJobTicket(job.Id, job.Kind, job.PayloadFormat, job.Payload))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrintJobTicket>> ListPendingSinceAsync(
        Guid tenantId,
        DateTime createdSinceUtc,
        CancellationToken cancellationToken = default,
        int skip = 0)
    {
        return await _db.PrintJobs.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(job => job.TenantId == tenantId &&
                          job.Status == PrintJob.StatusPending &&
                          job.CreatedAtUtc >= createdSinceUtc)
            .OrderBy(job => job.CreatedAtUtc)
            .ThenBy(job => job.Id)
            .Skip(skip < 0 ? 0 : skip)
            .Take(PendingLimit)
            .Select(job => new PrintJobTicket(job.Id, job.Kind, job.PayloadFormat, job.Payload))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AcknowledgeAsync(
        Guid jobId,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.PrintJobs.FirstOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job is null)
            return false;

        if (success)
        {
            job.Status = PrintJob.StatusPrinted;
            job.AcknowledgedAtUtc = DateTime.UtcNow;
            job.ErrorMessage = null;
        }
        else
        {
            job.ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
        }

        job.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
