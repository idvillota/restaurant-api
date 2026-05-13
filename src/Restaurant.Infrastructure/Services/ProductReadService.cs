using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Catalog;
using Restaurant.Domain.Entities;

namespace Restaurant.Infrastructure.Services;

public sealed class ProductReadService : IProductReadService
{
    private readonly IRepository<Product> _products;
    private readonly IMapper _mapper;

    public ProductReadService(IRepository<Product> products, IMapper mapper)
    {
        _products = products;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ProductListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _products.Query()
            .AsNoTracking()
            .Include(p => p.ProductType)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return _mapper.Map<IReadOnlyList<ProductListItemDto>>(rows);
    }
}
