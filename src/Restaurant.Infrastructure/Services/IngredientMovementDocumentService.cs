using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Inventory.IngredientMovements;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class IngredientMovementDocumentService : IIngredientMovementDocumentService
{
    private readonly IRepository<IngredientMovementDocument> _documents;
    private readonly IRepository<IngredientMovement> _lines;
    private readonly IRepository<Ingredient> _ingredients;
    private readonly IRepository<IngredientMovementType> _movementTypes;
    private readonly ICurrentTenantContext _tenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public IngredientMovementDocumentService(
        IRepository<IngredientMovementDocument> documents,
        IRepository<IngredientMovement> lines,
        IRepository<Ingredient> ingredients,
        IRepository<IngredientMovementType> movementTypes,
        ICurrentTenantContext tenant,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _documents = documents;
        _lines = lines;
        _ingredients = ingredients;
        _movementTypes = movementTypes;
        _tenant = tenant;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public Task<PagedResult<IngredientMovementDocumentListItemDto>> ListAsync(
        ListQuery query,
        CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync(
            _documents.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeIngredientMovementDocuments(q, query),
            entities => Task.FromResult<IReadOnlyList<IngredientMovementDocumentListItemDto>>(
                _mapper.Map<IReadOnlyList<IngredientMovementDocumentListItemDto>>(entities.ToList())),
            cancellationToken);

    public async Task<IngredientMovementDocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _documents.Query()
            .AsNoTracking()
            .Include(d => d.MovementType)
            .Include(d => d.CreatedByUser)
            .Include(d => d.Lines)
            .ThenInclude(l => l.Ingredient)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return entity is null ? null : _mapper.Map<IngredientMovementDocumentDto>(entity);
    }

    public async Task<IngredientMovementDocumentDto> CreateAsync(
        CreateIngredientMovementDocumentDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.Lines.Count == 0)
            throw new InvalidOperationException("El documento debe incluir al menos un ingrediente.");

        if (dto.Lines.Select(l => l.IngredientId).Distinct().Count() != dto.Lines.Count)
            throw new InvalidOperationException("Cada ingrediente solo puede aparecer una vez en el documento.");

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

        var movementType = await _movementTypes.GetByIdAsync(dto.IngredientMovementTypeId, cancellationToken);
        if (movementType is null || !movementType.IsActive)
            throw new InvalidOperationException("El tipo de movimiento no existe o está inactivo.");

        var ingredientIds = dto.Lines.Select(l => l.IngredientId).ToList();
        var ingredients = await _ingredients.Query()
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        foreach (var line in dto.Lines)
        {
            if (!ingredients.TryGetValue(line.IngredientId, out var ingredient) || !ingredient.IsActive)
                throw new InvalidOperationException("Uno de los ingredientes no existe o está inactivo.");
        }

        if (!movementType.IsInput)
        {
            foreach (var line in dto.Lines)
            {
                var ingredient = ingredients[line.IngredientId];
                var current = ingredient.StockQuantity ?? 0m;
                if (current < line.Quantity)
                    throw new InvalidOperationException($"Stock insuficiente para {ingredient.Name}.");
            }
        }

        var occurredAtUtc = DateTime.UtcNow;
        var document = new IngredientMovementDocument
        {
            Id = Guid.NewGuid(),
            IngredientMovementTypeId = movementType.Id,
            DocumentNumber = documentNumber,
            Notes = dto.Notes?.Trim(),
            CreatedByUserId = userId.Value,
            OccurredAtUtc = occurredAtUtc,
        };

        await _documents.AddAsync(document, cancellationToken);

        foreach (var lineDto in dto.Lines)
        {
            var ingredient = ingredients[lineDto.IngredientId];
            var stockBefore = ingredient.StockQuantity;

            if (movementType.IsInput)
                ingredient.StockQuantity = InventoryCosting.AddStock(ingredient.StockQuantity, lineDto.Quantity);
            else
                ingredient.StockQuantity = InventoryCosting.SubtractStock(ingredient.StockQuantity, lineDto.Quantity);

            _ingredients.Update(ingredient);

            await _lines.AddAsync(
                new IngredientMovement
                {
                    Id = Guid.NewGuid(),
                    IngredientMovementDocumentId = document.Id,
                    IngredientId = ingredient.Id,
                    Quantity = lineDto.Quantity,
                    StockQuantitySnapshot = stockBefore,
                    UnitCostSnapshot = ingredient.UnitCost,
                },
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(document.Id, cancellationToken))!;
    }
}
