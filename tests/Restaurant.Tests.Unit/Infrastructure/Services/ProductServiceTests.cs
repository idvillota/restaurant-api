using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Catalog.Products;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class ProductServiceTests
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
                IsActive = true,
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

        var sut = new ProductService(fx.Repository<Product>(), fx.Repository<ProductType>(), fx.UnitOfWork, fx.Mapper);
        var list = await sut.ListAsync();

        Assert.Single(list);
        Assert.Equal("Apple pie", list[0].Name);
        Assert.Equal("Food", list[0].ProductTypeName);
        Assert.True(list[0].IsActive);
    }

    [Fact]
    public async Task CreateAsync_throws_when_product_type_inactive()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(
            new ProductType
            {
                Id = typeId,
                TenantId = fx.TenantId,
                Name = "Inactive",
                SortOrder = 0,
                IsActive = false,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new ProductService(fx.Repository<Product>(), fx.Repository<ProductType>(), fx.UnitOfWork, fx.Mapper);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(
                new CreateProductDto { ProductTypeId = typeId, Name = "X", UnitPrice = 1m }));
    }

    [Fact]
    public async Task CreateAsync_persists_description_and_returns_it_in_list()
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
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new ProductService(fx.Repository<Product>(), fx.Repository<ProductType>(), fx.UnitOfWork, fx.Mapper);
        var created = await sut.CreateAsync(
            new CreateProductDto
            {
                ProductTypeId = typeId,
                Name = "House salad",
                Description = "  Fresh greens  ",
                UnitPrice = 8.5m,
            });

        Assert.Equal("Fresh greens", created.Description);

        var list = await sut.ListAsync();
        Assert.Single(list);
        Assert.Equal("Fresh greens", list[0].Description);
    }
}
