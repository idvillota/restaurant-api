using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Common;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Common;

public sealed class ProductLineCostCalculatorTests
{
    [Fact]
    public async Task ComputeUnitCostsAsync_sums_recipe_ingredient_costs()
    {
        using var fx = new TenantDbFixture();
        var categoryId = Guid.NewGuid();
        fx.Db.IngredientCategories.Add(
            new IngredientCategory { Id = categoryId, TenantId = fx.TenantId, Name = "Base", SortOrder = 0, IsActive = true });

        var flourId = Guid.NewGuid();
        var cheeseId = Guid.NewGuid();
        fx.Db.Ingredients.AddRange(
            new Ingredient
            {
                Id = flourId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Harina",
                Unit = IngredientUnit.Gram,
                UnitCost = 2m,
                IsActive = true,
            },
            new Ingredient
            {
                Id = cheeseId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Queso",
                Unit = IngredientUnit.Gram,
                UnitCost = 5m,
                IsActive = true,
            });

        var typeId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Pizza", SortOrder = 0, IsActive = true });

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
                IsActive = true,
            });

        fx.Db.ProductIngredients.AddRange(
            new ProductIngredient { Id = Guid.NewGuid(), TenantId = fx.TenantId, ProductId = productId, IngredientId = flourId, Quantity = 1m },
            new ProductIngredient { Id = Guid.NewGuid(), TenantId = fx.TenantId, ProductId = productId, IngredientId = cheeseId, Quantity = 2m });

        await fx.Db.SaveChangesAsync();

        var lineId = Guid.NewGuid();
        var costs = await ProductLineCostCalculator.ComputeUnitCostsAsync(
            fx.Db,
            [new ProductLineCostCalculator.LineCostSpec(lineId, productId, [])],
            CancellationToken.None);

        Assert.Equal(12m, costs[lineId]);
    }

    [Fact]
    public async Task ComputeUnitCostsAsync_excludes_ingredients_from_line()
    {
        using var fx = new TenantDbFixture();
        var categoryId = Guid.NewGuid();
        fx.Db.IngredientCategories.Add(
            new IngredientCategory { Id = categoryId, TenantId = fx.TenantId, Name = "Base", SortOrder = 0, IsActive = true });

        var flourId = Guid.NewGuid();
        var cheeseId = Guid.NewGuid();
        fx.Db.Ingredients.AddRange(
            new Ingredient
            {
                Id = flourId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Harina",
                Unit = IngredientUnit.Gram,
                UnitCost = 2m,
                IsActive = true,
            },
            new Ingredient
            {
                Id = cheeseId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Queso",
                Unit = IngredientUnit.Gram,
                UnitCost = 5m,
                IsActive = true,
            });

        var typeId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Pizza", SortOrder = 0, IsActive = true });

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
                IsActive = true,
            });

        fx.Db.ProductIngredients.AddRange(
            new ProductIngredient { Id = Guid.NewGuid(), TenantId = fx.TenantId, ProductId = productId, IngredientId = flourId, Quantity = 1m },
            new ProductIngredient { Id = Guid.NewGuid(), TenantId = fx.TenantId, ProductId = productId, IngredientId = cheeseId, Quantity = 2m });

        await fx.Db.SaveChangesAsync();

        var lineId = Guid.NewGuid();
        var costs = await ProductLineCostCalculator.ComputeUnitCostsAsync(
            fx.Db,
            [new ProductLineCostCalculator.LineCostSpec(lineId, productId, [cheeseId])],
            CancellationToken.None);

        Assert.Equal(2m, costs[lineId]);
    }
}
