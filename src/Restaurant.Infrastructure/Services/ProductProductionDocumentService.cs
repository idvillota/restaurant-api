using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Inventory.ProductProductions;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class ProductProductionDocumentService : IProductProductionDocumentService
{
    private readonly IRepository<ProductProductionDocument> _documents;
    private readonly IRepository<ProductProductionLine> _lines;
    private readonly IRepository<Product> _products;
    private readonly ICurrentTenantContext _tenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly Persistence.ApplicationDbContext _db;

    public ProductProductionDocumentService(
        IRepository<ProductProductionDocument> documents,
        IRepository<ProductProductionLine> lines,
        IRepository<Product> products,
        ICurrentTenantContext tenant,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        Persistence.ApplicationDbContext db)
    {
        _documents = documents;
        _lines = lines;
        _products = products;
        _tenant = tenant;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _db = db;
    }

    public Task<PagedResult<ProductProductionDocumentListItemDto>> ListAsync(
        ListQuery query,
        CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync(
            _documents.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeProductProductionDocuments(q, query),
            entities => Task.FromResult<IReadOnlyList<ProductProductionDocumentListItemDto>>(
                _mapper.Map<IReadOnlyList<ProductProductionDocumentListItemDto>>(entities.ToList())),
            cancellationToken);

    public async Task<ProductProductionDocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _documents.Query()
            .AsNoTracking()
            .Include(d => d.CreatedByUser)
            .Include(d => d.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return entity is null ? null : _mapper.Map<ProductProductionDocumentDto>(entity);
    }

    public async Task<ProductProductionDocumentDto> CreateAsync(
        CreateProductProductionDocumentDto dto,
        CancellationToken cancellationToken = default)
    {
        var mode = await StockControlHelper.GetModeAsync(_db, cancellationToken);
        if (mode != StockControlMode.ProductUnits)
            throw new InvalidOperationException(
                "La producción de productos solo está disponible cuando el control de stock es por unidades de producto.");

        if (dto.Lines.Count == 0)
            throw new InvalidOperationException("El documento debe incluir al menos un producto.");

        if (dto.Lines.Select(l => l.ProductId).Distinct().Count() != dto.Lines.Count)
            throw new InvalidOperationException("Cada producto solo puede aparecer una vez en el documento.");

        foreach (var line in dto.Lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("La cantidad de cada línea debe ser mayor que cero.");
        }

        var userId = _tenant.UserId;
        if (userId is null || userId == Guid.Empty)
            throw new InvalidOperationException("No se pudo determinar el usuario activo.");

        var documentNumber = dto.DocumentNumber.Trim();
        if (documentNumber.Length == 0)
            throw new InvalidOperationException("El número de documento es obligatorio.");

        var productIds = dto.Lines.Select(l => l.ProductId).ToList();
        var products = await _products.Query()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var line in dto.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product) || !product.IsActive)
                throw new InvalidOperationException("Uno de los productos no existe o está inactivo.");

            if (product.CompositionType == EProductType.Bundle)
                throw new InvalidOperationException(
                    $"No se puede producir el combo «{product.Name}». Produce sus componentes por separado.");
        }

        var occurredAtUtc = DateTime.UtcNow;
        var document = new ProductProductionDocument
        {
            Id = Guid.NewGuid(),
            DocumentNumber = documentNumber,
            Notes = dto.Notes?.Trim(),
            CreatedByUserId = userId.Value,
            OccurredAtUtc = occurredAtUtc,
        };

        await _documents.AddAsync(document, cancellationToken);

        foreach (var lineDto in dto.Lines)
        {
            var product = products[lineDto.ProductId];
            var stockBefore = product.StockQuantity;
            product.StockQuantity = InventoryCosting.AddStock(product.StockQuantity, lineDto.Quantity);
            _products.Update(product);

            await _lines.AddAsync(
                new ProductProductionLine
                {
                    Id = Guid.NewGuid(),
                    ProductProductionDocumentId = document.Id,
                    ProductId = product.Id,
                    Quantity = lineDto.Quantity,
                    StockQuantitySnapshot = stockBefore,
                },
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(document.Id, cancellationToken))!;
    }
}
