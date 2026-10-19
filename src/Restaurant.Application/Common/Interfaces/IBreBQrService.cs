using Restaurant.Application.Features.Sales.Bills;

namespace Restaurant.Application.Common.Interfaces;

public interface IBreBQrService
{
    Task<BreBQrDto> CreateAsync(decimal amount, CancellationToken cancellationToken = default);
}
