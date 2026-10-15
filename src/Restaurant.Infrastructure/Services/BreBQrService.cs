using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.Bills;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class BreBQrService : IBreBQrService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;

    public BreBQrService(ApplicationDbContext db, ICurrentTenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BreBQrDto> CreateAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is not available.");

        var settings = await _db.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        if (settings is null
            || string.IsNullOrWhiteSpace(settings.BreBKey)
            || settings.BreBKeyType is not { } keyType
            || !Enum.IsDefined(keyType))
        {
            throw new InvalidOperationException("Configura la llave Bre-B del local antes de mostrar el QR.");
        }

        if (string.IsNullOrWhiteSpace(settings.TradeName) || string.IsNullOrWhiteSpace(settings.City))
            throw new InvalidOperationException("Configura el nombre comercial y la ciudad del local para generar el QR.");

        var reference = Guid.NewGuid().ToString("N")[..10];
        var payload = BreBQrPayload.Build(new BreBQrInput(
            settings.BreBKey,
            keyType,
            amount,
            settings.TradeName,
            settings.City,
            settings.PostalCode,
            reference));

        return new BreBQrDto { Payload = payload };
    }
}
