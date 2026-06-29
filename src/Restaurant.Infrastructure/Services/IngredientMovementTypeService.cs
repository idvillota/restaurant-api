using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Inventory.IngredientMovementTypes;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class IngredientMovementTypeService : IIngredientMovementTypeService
{
    private readonly IRepository<IngredientMovementType> _types;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public IngredientMovementTypeService(
        IRepository<IngredientMovementType> types,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _types = types;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public Task<PagedResult<IngredientMovementTypeDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync(
            _types.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeIngredientMovementTypes(q, query),
            entities => Task.FromResult<IReadOnlyList<IngredientMovementTypeDto>>(
                _mapper.Map<IReadOnlyList<IngredientMovementTypeDto>>(entities.ToList())),
            cancellationToken);

    public async Task<IngredientMovementTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _types.Query().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<IngredientMovementTypeDto>(entity);
    }

    public async Task<IngredientMovementTypeDto> CreateAsync(
        CreateIngredientMovementTypeDto dto,
        CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        if (await _types.Query().AnyAsync(t => t.IsActive && t.Name == name, cancellationToken))
            throw new InvalidOperationException("Ya existe un tipo de movimiento activo con este nombre.");

        var entity = new IngredientMovementType
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = dto.Description?.Trim(),
            IsInput = dto.IsInput,
            SortOrder = dto.SortOrder,
            IsActive = true,
        };

        await _types.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<IngredientMovementTypeDto>(entity);
    }

    public async Task<IngredientMovementTypeDto?> UpdateAsync(
        Guid id,
        UpdateIngredientMovementTypeDto dto,
        CancellationToken cancellationToken = default)
    {
        var entity = await _types.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        var name = dto.Name.Trim();
        if (await _types.Query().AnyAsync(t => t.Id != id && t.IsActive && t.Name == name, cancellationToken))
            throw new InvalidOperationException("Otro tipo de movimiento activo ya usa este nombre.");

        entity.Name = name;
        entity.Description = dto.Description?.Trim();
        entity.IsInput = dto.IsInput;
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        _types.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<IngredientMovementTypeDto>(entity);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _types.GetByIdAsync(id, cancellationToken);
        if (entity is null || !entity.IsActive)
            return false;

        entity.IsActive = false;
        _types.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
