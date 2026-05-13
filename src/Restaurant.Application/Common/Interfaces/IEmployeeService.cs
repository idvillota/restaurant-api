using Restaurant.Application.Features.Organization.Employees;

namespace Restaurant.Application.Common.Interfaces;

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default);
    Task<EmployeeDto?> UpdateAsync(Guid id, UpdateEmployeeDto dto, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
