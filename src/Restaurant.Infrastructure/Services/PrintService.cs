using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class PrintService : IPrintService
{
    private readonly ApplicationDbContext _db;
    private readonly IGeneratedFileStorage _fileStorage;
    private readonly ICurrentTenantContext _tenant;
    private readonly ILogger<PrintService> _logger;

    public PrintService(
        ApplicationDbContext db,
        IGeneratedFileStorage fileStorage,
        ICurrentTenantContext tenant,
        ILogger<PrintService> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<GeneratedFileContent?> GetDocumentAsync(
        Guid id,
        PrintFileType type,
        string? overridePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("No se pudo determinar el tenant activo.");

        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            _logger.LogInformation("PrintService: using override path for tenant {Tenant} id {Id}", tenantId, id);
            return await _fileStorage.OpenReadAsync(overridePath, cancellationToken);
        }

        string? relativePath = null;
        switch (type)
        {
            case PrintFileType.Factura:
                relativePath = await _db.Bills
                    .AsNoTracking()
                    .Where(b => b.Id == id && b.TenantId == tenantId)
                    .Select(b => b.ReceiptXmlRelativePath)
                    .FirstOrDefaultAsync(cancellationToken);
                break;

            case PrintFileType.Comanda:
                // No persisted path by default; require override or upstream persistence.
                relativePath = null;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            _logger.LogInformation("PrintService: no path found for tenant {Tenant} id {Id} type {Type}", tenantId, id, type);
            return null;
        }

        _logger.LogInformation("PrintService: resolved path {Path} for tenant {Tenant} id {Id}", relativePath, tenantId, id);
        return await _fileStorage.OpenReadAsync(relativePath, cancellationToken);
    }
}
