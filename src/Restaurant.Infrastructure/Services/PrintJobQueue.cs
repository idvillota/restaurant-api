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

    public PrintJobQueue(ApplicationDbContext db, ICurrentTenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task EnqueueAsync(
        string kind,
        string payloadFormat,
        string payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payload) || _tenantContext.TenantId is not Guid tenantId)
            return;

        _db.PrintJobs.Add(new PrintJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kind = kind,
            PayloadFormat = payloadFormat,
            Payload = payload,
            Status = PrintJob.StatusPending,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
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
