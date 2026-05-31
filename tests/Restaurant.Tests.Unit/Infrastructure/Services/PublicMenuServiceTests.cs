using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Domain.Entities;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Infrastructure.Services;
using Restaurant.Infrastructure.Storage;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class PublicMenuServiceTests
{
    [Fact]
    public async Task GetByTenantSlug_returns_menu_grouped_by_category()
    {
        using var fx = new NoTenantDbFixture();
        var tenantId = Guid.NewGuid();
        var pizzasId = Guid.NewGuid();
        var drinksId = Guid.NewGuid();

        fx.Db.Tenants.Add(
            new Tenant
            {
                Id = tenantId,
                Name = "Bistró Demo",
                Slug = "demo-bistro",
                CurrencyCode = "COP",
                IsActive = true,
            });

        fx.Db.ProductTypes.AddRange(
            new ProductType
            {
                Id = pizzasId,
                TenantId = tenantId,
                Name = "Pizzas",
                SortOrder = 0,
                IsActive = true,
            },
            new ProductType
            {
                Id = drinksId,
                TenantId = tenantId,
                Name = "Refrescos",
                SortOrder = 1,
                IsActive = true,
            });

        fx.Db.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductTypeId = pizzasId,
                Name = "Pizza margarita",
                UnitPrice = 32000m,
                IsActive = true,
            },
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductTypeId = drinksId,
                Name = "Cola",
                UnitPrice = 6000m,
                IsActive = true,
            },
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductTypeId = drinksId,
                Name = "Inactive drink",
                UnitPrice = 1000m,
                IsActive = false,
            });

        await fx.Db.SaveChangesAsync();

        var imageStorage = new LocalProductImageStorage(
            Options.Create(new ProductImageOptions()),
            new FakeHostEnvironment());

        var sut = new PublicMenuService(fx.Db, imageStorage);

        var menu = await sut.GetByTenantSlugAsync("demo-bistro");

        Assert.NotNull(menu);
        Assert.Equal("Bistró Demo", menu.TenantName);
        Assert.Equal("demo-bistro", menu.TenantSlug);
        Assert.Equal(2, menu.Categories.Count);
        Assert.Equal("Pizzas", menu.Categories[0].Name);
        Assert.Single(menu.Categories[0].Products);
        Assert.Equal("Pizza margarita", menu.Categories[0].Products[0].Name);
        Assert.Equal("Refrescos", menu.Categories[1].Name);
        Assert.Single(menu.Categories[1].Products);
    }

    [Fact]
    public async Task GetByTenantSlug_returns_null_for_unknown_slug()
    {
        using var fx = new NoTenantDbFixture();
        var imageStorage = new LocalProductImageStorage(
            Options.Create(new ProductImageOptions()),
            new FakeHostEnvironment());
        var sut = new PublicMenuService(fx.Db, imageStorage);

        var menu = await sut.GetByTenantSlugAsync("missing-tenant");

        Assert.Null(menu);
    }

    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
