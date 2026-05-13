using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Organization.Employees;
using Restaurant.Domain.Entities;

namespace Restaurant.Infrastructure.Services;

public sealed class EmployeeService : IEmployeeService
{
    private readonly IRepository<Employee> _employees;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public EmployeeService(IRepository<Employee> employees, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _employees = employees;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<EmployeeDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _employees.Query().AsNoTracking().OrderBy(e => e.FullName);
        var list = includeInactive
            ? await query.ToListAsync(cancellationToken)
            : await query.Where(e => e.IsActive).ToListAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<EmployeeDto>>(list);
    }

    public async Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _employees.Query().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<EmployeeDto>(entity);
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new Employee
        {
            Id = Guid.NewGuid(),
            TenantUserId = dto.TenantUserId,
            FullName = dto.FullName.Trim(),
            JobTitle = dto.JobTitle?.Trim(),
            HiredOn = dto.HiredOn,
            IsActive = true,
        };
        await _employees.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<EmployeeDto>(entity);
    }

    public async Task<EmployeeDto?> UpdateAsync(Guid id, UpdateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _employees.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        entity.TenantUserId = dto.TenantUserId;
        entity.FullName = dto.FullName.Trim();
        entity.JobTitle = dto.JobTitle?.Trim();
        entity.HiredOn = dto.HiredOn;
        entity.IsActive = dto.IsActive;
        _employees.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<EmployeeDto>(entity);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _employees.GetByIdAsync(id, cancellationToken);
        if (entity is null || !entity.IsActive)
            return false;

        entity.IsActive = false;
        _employees.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
