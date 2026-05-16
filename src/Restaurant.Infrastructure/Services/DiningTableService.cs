using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Operations.DiningTables;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Common;

namespace Restaurant.Infrastructure.Services;

public sealed class DiningTableService : IDiningTableService
{
    private readonly IRepository<DiningTable> _tables;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public DiningTableService(IRepository<DiningTable> tables, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _tables = tables;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public Task<PagedResult<DiningTableDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default) =>
        ListQueryHelpers.ToPagedResultAsync(
            _tables.Query().AsNoTracking(),
            query,
            q => PagedEntityQueries.ShapeDiningTables(q, query),
            entities => Task.FromResult<IReadOnlyList<DiningTableDto>>(_mapper.Map<IReadOnlyList<DiningTableDto>>(entities.ToList())),
            cancellationToken);

    public async Task<DiningTableDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _tables.Query().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<DiningTableDto>(entity);
    }

    public async Task<DiningTableDto> CreateAsync(CreateDiningTableDto dto, CancellationToken cancellationToken = default)
    {
        var code = dto.Code.Trim();
        if (await _tables.Query().AnyAsync(t => t.IsActive && t.Code == code, cancellationToken))
            throw new InvalidOperationException("An active table with this code already exists.");

        var entity = new DiningTable
        {
            Id = Guid.NewGuid(),
            Code = code,
            Capacity = dto.Capacity,
            Zone = dto.Zone?.Trim(),
            IsActive = true,
        };
        await _tables.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<DiningTableDto>(entity);
    }

    public async Task<DiningTableDto?> UpdateAsync(Guid id, UpdateDiningTableDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _tables.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        var code = dto.Code.Trim();
        if (await _tables.Query().AnyAsync(t => t.Id != id && t.IsActive && t.Code == code, cancellationToken))
            throw new InvalidOperationException("Another active table already uses this code.");

        entity.Code = code;
        entity.Capacity = dto.Capacity;
        entity.Zone = dto.Zone?.Trim();
        entity.IsActive = dto.IsActive;
        _tables.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<DiningTableDto>(entity);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _tables.GetByIdAsync(id, cancellationToken);
        if (entity is null || !entity.IsActive)
            return false;

        entity.IsActive = false;
        _tables.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
