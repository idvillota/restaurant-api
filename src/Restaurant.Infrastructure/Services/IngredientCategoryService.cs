using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog.IngredientCategories;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class IngredientCategoryService : IIngredientCategoryService
{
    private readonly IRepository<IngredientCategory> _categories;
    private readonly IRepository<Ingredient> _ingredients;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public IngredientCategoryService(
        IRepository<IngredientCategory> categories,
        IRepository<Ingredient> ingredients,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _categories = categories;
        _ingredients = ingredients;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public Task<PagedResult<IngredientCategoryDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync(
            _categories.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeIngredientCategories(q, query),
            entities => Task.FromResult<IReadOnlyList<IngredientCategoryDto>>(_mapper.Map<IReadOnlyList<IngredientCategoryDto>>(entities.ToList())),
            cancellationToken);

    public async Task<IngredientCategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _categories.Query().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<IngredientCategoryDto>(entity);
    }

    public async Task<IngredientCategoryDto> CreateAsync(CreateIngredientCategoryDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        if (await _categories.Query().AnyAsync(c => c.IsActive && c.Name == name, cancellationToken))
            throw new InvalidOperationException("An active ingredient category with this name already exists.");

        var entity = new IngredientCategory
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = dto.Description?.Trim(),
            SortOrder = dto.SortOrder,
            IsActive = true,
        };
        await _categories.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<IngredientCategoryDto>(entity);
    }

    public async Task<IngredientCategoryDto?> UpdateAsync(Guid id, UpdateIngredientCategoryDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _categories.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        var name = dto.Name.Trim();
        if (await _categories.Query().AnyAsync(c => c.Id != id && c.IsActive && c.Name == name, cancellationToken))
            throw new InvalidOperationException("Another active ingredient category already uses this name.");

        entity.Name = name;
        entity.Description = dto.Description?.Trim();
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        _categories.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<IngredientCategoryDto>(entity);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _categories.GetByIdAsync(id, cancellationToken);
        if (entity is null || !entity.IsActive)
            return false;

        if (await _ingredients.Query().AnyAsync(i => i.IngredientCategoryId == id && i.IsActive, cancellationToken))
            throw new InvalidOperationException("Cannot deactivate a category that still has active ingredients.");

        entity.IsActive = false;
        _categories.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
