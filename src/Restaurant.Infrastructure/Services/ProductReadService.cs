using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Catalog;
using Restaurant.Infrastructure.Persistence;

namespace Restaurant.Infrastructure.Services;

public sealed class ProductReadService : IProductReadService
{
    private readonly ApplicationDbContext _db;
    private readonly IMapper _mapper;

    public ProductReadService(ApplicationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ProductListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Products
            .AsNoTracking()
            .Include(p => p.ProductType)
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return _mapper.Map<IReadOnlyList<ProductListItemDto>>(rows);
    }
}
