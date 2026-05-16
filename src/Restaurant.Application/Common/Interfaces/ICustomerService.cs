using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Crm.Customers;

namespace Restaurant.Application.Common.Interfaces;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> ListAsync(ListQuery query, CancellationToken cancellationToken = default);
    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken cancellationToken = default);
    Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerDto dto, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
