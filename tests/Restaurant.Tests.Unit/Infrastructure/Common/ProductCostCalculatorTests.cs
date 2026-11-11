using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Common;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Common;

public sealed class ProductCostCalculatorTests
{
    [Fact]
    public async Task GetCostPrices_uses_manual_cost_when_inventory_disabled_even_if_recipe_exists()
    {
        using var fx = new TenantDbFixture();
        var (productId, _) = await SeedPreparedWithRecipeAsync(fx, manualCost: 9.25m);
        fx.Db.TenantSettings.Add(
            new TenantSettings { TenantId = fx.TenantId, InventoryEnabled = false });
        await fx.Db.SaveChangesAsync();

        var costs = await ProductCostCalculator.GetCostPricesByProductIdsAsync(
            fx.Db, [productId], CancellationToken.None);

        Assert.Equal(9.25m, costs[productId]);
    }

    [Fact]
    public async Task GetCostPrices_uses_recipe_when_inventory_enabled()
    {
        using var fx = new TenantDbFixture();
        var (productId, recipeCost) = await SeedPreparedWithRecipeAsync(fx, manualCost: 9.25m);
        await fx.Db.SaveChangesAsync();

        var costs = await ProductCostCalculator.GetCostPricesByProductIdsAsync(
            fx.Db, [productId], CancellationToken.None);

        Assert.Equal(recipeCost, costs[productId]);
    }

    [Fact]
    public async Task GetCostPrices_uses_combo_manual_cost_not_component_sum_when_inventory_disabled()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Menu", IsActive = true });

        var componentId = Guid.NewGuid();
        var comboId = Guid.NewGuid();
        fx.Db.Products.AddRange(
            new Product
            {
                Id = componentId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Burger",
                CompositionType = EProductType.Prepared,
                UnitPrice = 18m,
                ManualCostPrice = 6m,
                IsActive = true,
            },
            new Product
            {
                Id = comboId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Combo",
                CompositionType = EProductType.Bundle,
                UnitPrice = 22m,
                ManualCostPrice = 8.5m,
                IsActive = true,
            });
        fx.Db.ProductBundleLines.Add(
            new ProductBundleLine
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductId = comboId,
                ComponentProductId = componentId,
                Quantity = 2m,
                SortOrder = 0,
            });
        fx.Db.TenantSettings.Add(
            new TenantSettings { TenantId = fx.TenantId, InventoryEnabled = false });
        await fx.Db.SaveChangesAsync();

        var costs = await ProductCostCalculator.GetCostPricesByProductIdsAsync(
            fx.Db, [comboId], CancellationToken.None);

        Assert.Equal(8.5m, costs[comboId]);
    }

    private static async Task<(Guid ProductId, decimal RecipeCost)> SeedPreparedWithRecipeAsync(
        TenantDbFixture fx,
        decimal manualCost)
    {
        var categoryId = Guid.NewGuid();
        fx.Db.IngredientCategories.Add(
            new IngredientCategory { Id = categoryId, TenantId = fx.TenantId, Name = "Base", IsActive = true });

        var ingredientId = Guid.NewGuid();
        fx.Db.Ingredients.Add(
            new Ingredient
            {
                Id = ingredientId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Harina",
                Unit = IngredientUnit.Gram,
                UnitCost = 2m,
                IsActive = true,
            });

        var typeId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Pizza", IsActive = true });

        var productId = Guid.NewGuid();
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Margarita",
                CompositionType = EProductType.Prepared,
                UnitPrice = 20m,
                ManualCostPrice = manualCost,
                IsActive = true,
            });
        fx.Db.ProductIngredients.Add(
            new ProductIngredient
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductId = productId,
                IngredientId = ingredientId,
                Quantity = 3m,
            });

        await Task.CompletedTask;
        return (productId, 6m);
    }
}
