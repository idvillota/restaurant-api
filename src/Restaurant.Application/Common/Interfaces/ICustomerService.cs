using Restaurant.Application.Features.Crm.Customers;

namespace Restaurant.Application.Common.Interfaces;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken cancellationToken = default);
    Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerDto dto, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
