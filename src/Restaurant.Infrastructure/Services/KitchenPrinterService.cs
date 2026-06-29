using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.KitchenPrinters;
using Restaurant.Application.Features.Sales.SalesOrders;
using Restaurant.Domain.Common;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class KitchenPrinterService : IKitchenPrinterService
{
    private static readonly Regex StationCodePattern = new("^[A-Z0-9]{2,20}$", RegexOptions.CultureInvariant);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;

    public KitchenPrinterService(ApplicationDbContext db, ICurrentTenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task EnsureDefaultStationAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.PrinterStations.IgnoreQueryFilters()
            .AnyAsync(
                s => s.TenantId == tenantId && s.Code == KitchenPrinterDefaults.DefaultStationCode,
                cancellationToken);

        if (exists)
            return;

        await _db.PrinterStations.AddAsync(
            new PrinterStation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = KitchenPrinterDefaults.DefaultStationName,
                Code = KitchenPrinterDefaults.DefaultStationCode,
                IsActive = true,
                SortOrder = 0,
            },
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrinterStationDto>> ListStationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        await EnsureDefaultStationAsync(tenantId, cancellationToken);

        var stations = await _db.PrinterStations.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return stations.Select(MapStation).ToList();
    }

    public async Task<PrinterStationDto> CreateStationAsync(
        CreatePrinterStationDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        await EnsureDefaultStationAsync(tenantId, cancellationToken);

        var code = NormalizeStationCode(dto.Code);
        if (code == KitchenPrinterDefaults.DefaultStationCode)
            throw new InvalidOperationException("El código DEFAULT está reservado para la estación del sistema.");

        if (await StationCodeExistsAsync(tenantId, code, excludeId: null, cancellationToken))
            throw new InvalidOperationException($"Ya existe una estación con el código {code}.");

        var station = new PrinterStation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name.Trim(),
            Code = code,
            IsActive = true,
            SortOrder = dto.SortOrder,
        };

        await _db.PrinterStations.AddAsync(station, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return MapStation(station);
    }

    public async Task<PrinterStationDto?> UpdateStationAsync(
        Guid id,
        UpdatePrinterStationDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var station = await _db.PrinterStations.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (station is null)
            return null;

        var isDefault = station.Code == KitchenPrinterDefaults.DefaultStationCode;
        station.Name = dto.Name.Trim();
        station.IsActive = dto.IsActive;
        station.SortOrder = dto.SortOrder;

        if (!isDefault && !string.IsNullOrWhiteSpace(dto.Code))
        {
            var code = NormalizeStationCode(dto.Code);
            if (code == KitchenPrinterDefaults.DefaultStationCode)
                throw new InvalidOperationException("El código DEFAULT está reservado para la estación del sistema.");

            if (await StationCodeExistsAsync(tenantId, code, station.Id, cancellationToken))
                throw new InvalidOperationException($"Ya existe una estación con el código {code}.");

            station.Code = code;
        }

        if (isDefault && dto.IsActive == false)
            throw new InvalidOperationException("La estación DEFAULT no puede desactivarse.");

        await _db.SaveChangesAsync(cancellationToken);
        return MapStation(station);
    }

    public async Task<bool> SoftDeleteStationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var station = await _db.PrinterStations.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (station is null)
            return false;

        if (station.Code == KitchenPrinterDefaults.DefaultStationCode)
            throw new InvalidOperationException("La estación DEFAULT no puede eliminarse.");

        var mappings = await _db.ProductTypePrinterMappings
            .Where(m => m.PrinterStationId == id)
            .ToListAsync(cancellationToken);
        _db.ProductTypePrinterMappings.RemoveRange(mappings);

        station.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ProductTypePrinterRoutingDto> GetRoutingAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        await EnsureDefaultStationAsync(tenantId, cancellationToken);

        var stations = await ListStationsAsync(cancellationToken);
        var productTypes = await _db.ProductTypes.AsNoTracking()
            .Where(pt => pt.TenantId == tenantId)
            .OrderBy(pt => pt.SortOrder)
            .ThenBy(pt => pt.Name)
            .ToListAsync(cancellationToken);

        var mappings = await _db.ProductTypePrinterMappings.AsNoTracking()
            .Include(m => m.PrinterStation)
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var mappingByType = mappings.ToDictionary(m => m.ProductTypeId);

        var items = productTypes.Select(pt =>
        {
            mappingByType.TryGetValue(pt.Id, out var mapping);
            return new ProductTypePrinterRoutingItemDto
            {
                ProductTypeId = pt.Id,
                ProductTypeName = pt.Name,
                ProductTypeIsActive = pt.IsActive,
                PrinterStationId = mapping?.PrinterStationId,
                PrinterStationName = mapping?.PrinterStation.Name,
                PrinterStationCode = mapping?.PrinterStation.Code,
            };
        }).ToList();

        return new ProductTypePrinterRoutingDto
        {
            Stations = stations,
            Items = items,
        };
    }

    public async Task<ProductTypePrinterRoutingDto> UpdateRoutingAsync(
        UpdateProductTypePrinterRoutingDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        await EnsureDefaultStationAsync(tenantId, cancellationToken);

        var productTypeIds = await _db.ProductTypes.AsNoTracking()
            .Where(pt => pt.TenantId == tenantId)
            .Select(pt => pt.Id)
            .ToListAsync(cancellationToken);
        var productTypeSet = productTypeIds.ToHashSet();

        var stationIds = await _db.PrinterStations.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        var stationSet = stationIds.ToHashSet();

        var existing = await _db.ProductTypePrinterMappings
            .Where(m => m.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var existingByType = existing.ToDictionary(m => m.ProductTypeId);

        foreach (var item in dto.Items)
        {
            if (!productTypeSet.Contains(item.ProductTypeId))
                throw new InvalidOperationException("Tipo de producto no válido para este local.");

            if (item.PrinterStationId is null)
            {
                if (existingByType.Remove(item.ProductTypeId, out var toRemove))
                    _db.ProductTypePrinterMappings.Remove(toRemove);
                continue;
            }

            if (!stationSet.Contains(item.PrinterStationId.Value))
                throw new InvalidOperationException("Estación de impresión no válida.");

            if (existingByType.TryGetValue(item.ProductTypeId, out var current))
            {
                current.PrinterStationId = item.PrinterStationId.Value;
                continue;
            }

            var mapping = new ProductTypePrinterMapping
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductTypeId = item.ProductTypeId,
                PrinterStationId = item.PrinterStationId.Value,
            };
            await _db.ProductTypePrinterMappings.AddAsync(mapping, cancellationToken);
            existingByType[item.ProductTypeId] = mapping;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetRoutingAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, (Guid StationId, string StationName, List<AddSalesOrderLineDto> Lines)>> GroupBatchByStationAsync(
        IReadOnlyList<AddSalesOrderLineDto> batchLines,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        await EnsureDefaultStationAsync(tenantId, cancellationToken);

        var productIds = batchLines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ProductTypeId })
            .ToListAsync(cancellationToken);

        var productTypeByProduct = products.ToDictionary(p => p.Id, p => p.ProductTypeId);
        if (productTypeByProduct.Count != productIds.Count)
            throw new InvalidOperationException("Product was not found or is inactive.");

        var mappings = await _db.ProductTypePrinterMappings.AsNoTracking()
            .Include(m => m.PrinterStation)
            .Where(m => m.TenantId == tenantId && m.PrinterStation.IsActive)
            .ToListAsync(cancellationToken);
        var stationByType = mappings.ToDictionary(m => m.ProductTypeId, m => m.PrinterStation);

        var defaultStation = await _db.PrinterStations.AsNoTracking()
            .FirstAsync(
                s => s.TenantId == tenantId && s.Code == KitchenPrinterDefaults.DefaultStationCode,
                cancellationToken);

        var groups = new Dictionary<string, (Guid StationId, string StationName, List<AddSalesOrderLineDto> Lines)>(
            StringComparer.Ordinal);

        foreach (var line in batchLines)
        {
            var productTypeId = productTypeByProduct[line.ProductId];
            var station = stationByType.GetValueOrDefault(productTypeId) ?? defaultStation;
            var code = station.Code;

            if (!groups.TryGetValue(code, out var group))
            {
                group = (station.Id, station.Name, []);
                groups[code] = group;
            }

            group.Lines.Add(line);
            groups[code] = group;
        }

        return groups;
    }

    private async Task<bool> StationCodeExistsAsync(
        Guid tenantId,
        string code,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var query = _db.PrinterStations.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && s.Code == code);

        if (excludeId.HasValue)
            query = query.Where(s => s.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    private static string NormalizeStationCode(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        normalized = Regex.Replace(normalized, @"[^A-Z0-9]", "");
        if (!StationCodePattern.IsMatch(normalized))
            throw new InvalidOperationException("El código debe tener entre 2 y 20 caracteres alfanuméricos (A-Z, 0-9).");
        return normalized;
    }

    private Guid ResolveTenantId() =>
        _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");

    private static PrinterStationDto MapStation(PrinterStation station) => new()
    {
        Id = station.Id,
        Name = station.Name,
        Code = station.Code,
        IsActive = station.IsActive,
        SortOrder = station.SortOrder,
        IsSystemDefault = station.Code == KitchenPrinterDefaults.DefaultStationCode,
    };
}
