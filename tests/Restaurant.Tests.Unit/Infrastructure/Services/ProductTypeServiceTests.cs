using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Catalog.ProductTypes;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class ProductTypeServiceTests
{
    [Fact]
    public async Task SoftDelete_throws_when_active_products_exist()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(
            new ProductType
            {
                Id = typeId,
                TenantId = fx.TenantId,
                Name = "Drinks",
                SortOrder = 0,
                IsActive = true,
            });
        fx.Db.Products.Add(
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Water",
                UnitPrice = 1m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new ProductTypeService(
            fx.Repository<ProductType>(),
            fx.Repository<Product>(),
            fx.UnitOfWork,
            fx.Mapper);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SoftDeleteAsync(typeId));
    }
}
