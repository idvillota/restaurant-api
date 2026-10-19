using Restaurant.Application.Features.Inventory;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class InventoryAvailabilityServiceTests
{
    [Fact]
    public async Task CheckLinesAsync_resale_without_recipe_is_available()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Drinks", IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Soda",
                CompositionType = EProductType.Resale,
                UnitPrice = 3m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new InventoryAvailabilityService(fx.Db);
        var result = await sut.CheckLinesAsync(
            new StockAvailabilityCheckDto
            {
                Lines =
                [
                    new StockCheckLineDto { ProductId = productId, Quantity = 2m },
                ],
            });

        Assert.True(result.IsAvailable);
        Assert.Empty(result.Shortages);
    }

    [Fact]
    public async Task CheckLinesAsync_skips_demand_when_inventory_disabled()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        fx.Db.TenantSettings.Add(
            new TenantSettings { TenantId = fx.TenantId, InventoryEnabled = false });
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Drinks", IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Beer",
                CompositionType = EProductType.Resale,
                UnitPrice = 5m,
                IsActive = true,
            });
        fx.Db.IngredientCategories.Add(
            new IngredientCategory { Id = categoryId, TenantId = fx.TenantId, Name = "Bev", IsActive = true });
        fx.Db.Ingredients.Add(
            new Ingredient
            {
                Id = ingredientId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Beer bottle",
                Unit = IngredientUnit.Unit,
                UnitCost = 2m,
                StockQuantity = 0m,
                IsActive = true,
            });
        fx.Db.ProductIngredients.Add(
            new ProductIngredient
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductId = productId,
                IngredientId = ingredientId,
                Quantity = 1m,
            });
        await fx.Db.SaveChangesAsync();

        var sut = new InventoryAvailabilityService(fx.Db);
        var result = await sut.CheckLinesAsync(
            new StockAvailabilityCheckDto
            {
                Lines =
                [
                    new StockCheckLineDto { ProductId = productId, Quantity = 1m },
                ],
            });

        Assert.True(result.IsAvailable);
        Assert.Empty(result.Shortages);
    }
}
