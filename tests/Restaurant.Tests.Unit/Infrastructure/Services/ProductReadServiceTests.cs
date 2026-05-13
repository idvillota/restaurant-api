using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class ProductReadServiceTests
{
    [Fact]
    public async Task ListAsync_returns_active_products_ordered_by_name()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(
            new ProductType
            {
                Id = typeId,
                TenantId = fx.TenantId,
                Name = "Food",
                SortOrder = 0,
            });
        fx.Db.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Zebra cake",
                UnitPrice = 10m,
                IsActive = false,
            },
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Apple pie",
                UnitPrice = 12m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new ProductReadService(fx.Repository<Product>(), fx.Mapper);
        var list = await sut.ListAsync();

        Assert.Single(list);
        Assert.Equal("Apple pie", list[0].Name);
        Assert.Equal("Food", list[0].ProductTypeName);
    }
}
