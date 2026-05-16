using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog.Products;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class ProductServiceTests
{
    private static ProductService CreateSut(TenantDbFixture fx) =>
        new(
            fx.Repository<Product>(),
            fx.Repository<ProductType>(),
            fx.Repository<ProductIngredient>(),
            fx.Repository<Ingredient>(),
            fx.UnitOfWork,
            fx.Mapper);

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

        var sut = CreateSut(fx);
        var list = await sut.ListAsync(new ListQuery { Page = 1, PageSize = 100 });

        Assert.Single(list.Items);
        Assert.Equal("Apple pie", list.Items[0].Name);
        Assert.Equal("Food", list.Items[0].ProductTypeName);
        Assert.True(list.Items[0].IsActive);
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

        var sut = CreateSut(fx);

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

        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            new CreateProductDto
            {
                ProductTypeId = typeId,
                Name = "House salad",
                Description = "  Fresh greens  ",
                UnitPrice = 8.5m,
            });

        Assert.Equal("Fresh greens", created.Description);

        var list = await sut.ListAsync(new ListQuery { Page = 1, PageSize = 100 });
        Assert.Single(list.Items);
        Assert.Equal("Fresh greens", list.Items[0].Description);
    }

    [Fact]
    public async Task SetRecipeAsync_persists_lines_and_computes_cost_price()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var flourId = Guid.NewGuid();
        var sugarId = Guid.NewGuid();

        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", SortOrder = 0, IsActive = true });
        fx.Db.IngredientCategories.Add(
            new IngredientCategory { Id = categoryId, TenantId = fx.TenantId, Name = "Dry", SortOrder = 0, IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Cake",
                UnitPrice = 20m,
                IsActive = true,
            });
        fx.Db.Ingredients.AddRange(
            new Ingredient
            {
                Id = flourId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Flour",
                Unit = IngredientUnit.Kilogram,
                UnitCost = 2m,
                IsActive = true,
            },
            new Ingredient
            {
                Id = sugarId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Sugar",
                Unit = IngredientUnit.Kilogram,
                UnitCost = 3m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        var recipe = await sut.SetRecipeAsync(
            productId,
            new SetProductRecipeDto
            {
                Lines =
                [
                    new SetProductRecipeLineDto { IngredientId = flourId, Quantity = 0.5m },
                    new SetProductRecipeLineDto { IngredientId = sugarId, Quantity = 0.25m },
                ],
            });

        Assert.NotNull(recipe);
        Assert.Equal(2, recipe!.Lines.Count);
        Assert.Equal(1.75m, recipe.CostPrice);
        Assert.Equal(1m, recipe.Lines.First(l => l.IngredientId == flourId).LineCost);
        Assert.Equal(0.75m, recipe.Lines.First(l => l.IngredientId == sugarId).LineCost);

        var loaded = await sut.GetRecipeAsync(productId);
        Assert.NotNull(loaded);
        Assert.Equal(1.75m, loaded!.CostPrice);

        var list = await sut.ListAsync(new ListQuery { Page = 1, PageSize = 100, IncludeInactive = true });
        Assert.Equal(1.75m, list.Items.Single(p => p.Id == productId).CostPrice);
    }

    [Fact]
    public async Task SetRecipeAsync_throws_when_quantity_is_zero()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", SortOrder = 0, IsActive = true });
        fx.Db.IngredientCategories.Add(
            new IngredientCategory { Id = categoryId, TenantId = fx.TenantId, Name = "Dry", SortOrder = 0, IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Cake",
                UnitPrice = 20m,
                IsActive = true,
            });
        fx.Db.Ingredients.Add(
            new Ingredient
            {
                Id = ingredientId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Flour",
                Unit = IngredientUnit.Kilogram,
                UnitCost = 2m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetRecipeAsync(
                productId,
                new SetProductRecipeDto
                {
                    Lines = [new SetProductRecipeLineDto { IngredientId = ingredientId, Quantity = 0m }],
                }));
    }
}
