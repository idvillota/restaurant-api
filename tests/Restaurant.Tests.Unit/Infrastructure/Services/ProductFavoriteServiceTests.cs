using Microsoft.EntityFrameworkCore;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class ProductFavoriteServiceTests
{
    [Fact]
    public async Task AddAsync_stores_favorite_and_is_idempotent()
    {
        using var fx = new TenantDbFixture();
        var (user, product) = await SeedAsync(fx);
        fx.TenantContext.UserId = user.Id;
        var sut = new ProductFavoriteService(fx.Db, fx.TenantContext);

        var first = await sut.AddAsync(product.Id);
        var second = await sut.AddAsync(product.Id);

        Assert.Equal(product.Id, Assert.Single(first.ProductIds));
        Assert.Equal(product.Id, Assert.Single(second.ProductIds));
        Assert.Equal(1, await fx.Db.TenantUserProductFavorites.CountAsync());
    }

    [Fact]
    public async Task AddAsync_throws_at_max_of_ten()
    {
        using var fx = new TenantDbFixture();
        var (user, firstProduct) = await SeedAsync(fx);
        fx.TenantContext.UserId = user.Id;
        var typeId = firstProduct.ProductTypeId;
        for (var i = 0; i < 9; i++)
        {
            fx.Db.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = $"P{i}",
                UnitPrice = 1,
                IsActive = true,
            });
        }
        await fx.Db.SaveChangesAsync();
        var sut = new ProductFavoriteService(fx.Db, fx.TenantContext);
        foreach (var id in await fx.Db.Products.Select(p => p.Id).ToListAsync())
            await sut.AddAsync(id);

        var extra = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            ProductTypeId = typeId,
            Name = "Extra",
            UnitPrice = 1,
            IsActive = true,
        };
        fx.Db.Products.Add(extra);
        await fx.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AddAsync(extra.Id));
        Assert.Contains("10", ex.Message);
    }

    [Fact]
    public async Task ListAsync_omits_inactive_products()
    {
        using var fx = new TenantDbFixture();
        var (user, product) = await SeedAsync(fx);
        fx.TenantContext.UserId = user.Id;
        var sut = new ProductFavoriteService(fx.Db, fx.TenantContext);
        await sut.AddAsync(product.Id);
        product.IsActive = false;
        await fx.Db.SaveChangesAsync();

        var list = await sut.ListAsync();

        Assert.Empty(list.ProductIds);
        Assert.Equal(0, await fx.Db.TenantUserProductFavorites.CountAsync());
    }

    [Fact]
    public async Task RemoveAsync_deletes_favorite()
    {
        using var fx = new TenantDbFixture();
        var (user, product) = await SeedAsync(fx);
        fx.TenantContext.UserId = user.Id;
        var sut = new ProductFavoriteService(fx.Db, fx.TenantContext);
        await sut.AddAsync(product.Id);

        var list = await sut.RemoveAsync(product.Id);

        Assert.Empty(list.ProductIds);
    }

    private static async Task<(User User, Product Product)> SeedAsync(TenantDbFixture fx)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "waiter@example.com",
            NormalizedEmail = "WAITER@EXAMPLE.COM",
            PasswordHash = "hash",
        };
        var membership = new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            UserId = user.Id,
            IsActive = true,
        };
        var type = new ProductType
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            Name = "Bebidas",
            IsActive = true,
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            ProductTypeId = type.Id,
            Name = "Agua",
            UnitPrice = 2000,
            IsActive = true,
        };
        fx.Db.Users.Add(user);
        fx.Db.TenantUsers.Add(membership);
        fx.Db.ProductTypes.Add(type);
        fx.Db.Products.Add(product);
        await fx.Db.SaveChangesAsync();
        return (user, product);
    }
}
