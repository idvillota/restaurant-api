using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog.Products;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class ProductServiceTests
{
    private static ProductService CreateSut(TenantDbFixture fx, FakeProductImageStorage? images = null)
    {
        images ??= new FakeProductImageStorage();
        return new ProductService(
            fx.Repository<Product>(),
            fx.Repository<ProductType>(),
            fx.Repository<ProductIngredient>(),
            fx.Repository<ProductBundleLine>(),
            fx.Repository<Ingredient>(),
            fx.TenantContext,
            images,
            fx.UnitOfWork,
            fx.Mapper);
    }

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
        Assert.Equal(EProductType.Prepared, list.Items[0].CompositionType);
        Assert.True(list.Items[0].IsActive);
    }

    [Fact]
    public async Task ListAsync_filters_by_composition_type()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", SortOrder = 0, IsActive = true });
        fx.Db.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Burger",
                CompositionType = EProductType.Prepared,
                UnitPrice = 10m,
                IsActive = true,
            },
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Beer",
                CompositionType = EProductType.Resale,
                UnitPrice = 5m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        var resaleOnly = await sut.ListAsync(
            new ListQuery
            {
                Page = 1,
                PageSize = 100,
                Filters = new Dictionary<string, string> { ["compositionType"] = "1" },
            });

        Assert.Single(resaleOnly.Items);
        Assert.Equal("Beer", resaleOnly.Items[0].Name);
        Assert.Equal(EProductType.Resale, resaleOnly.Items[0].CompositionType);

        var preparedOnly = await sut.ListAsync(
            new ListQuery
            {
                Page = 1,
                PageSize = 100,
                Filters = new Dictionary<string, string> { ["compositionType"] = "0" },
            });

        Assert.Single(preparedOnly.Items);
        Assert.Equal("Burger", preparedOnly.Items[0].Name);
        Assert.Equal(EProductType.Prepared, preparedOnly.Items[0].CompositionType);
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
    public async Task CreateAsync_resale_with_ingredient_creates_recipe_in_one_step()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var beerId = Guid.NewGuid();

        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Drinks", SortOrder = 0, IsActive = true });
        fx.Db.IngredientCategories.Add(
            new IngredientCategory { Id = categoryId, TenantId = fx.TenantId, Name = "Beverages", SortOrder = 0, IsActive = true });
        fx.Db.Ingredients.Add(
            new Ingredient
            {
                Id = beerId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Beer",
                Unit = IngredientUnit.Unit,
                UnitCost = 2.5m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            new CreateProductDto
            {
                ProductTypeId = typeId,
                Name = "Beer pint",
                CompositionType = EProductType.Resale,
                UnitPrice = 6m,
                ResaleIngredientId = beerId,
                ResaleQuantity = 1m,
            });

        Assert.Equal(EProductType.Resale, created.CompositionType);
        Assert.Equal(2.5m, created.CostPrice);

        var recipe = await sut.GetRecipeAsync(created.Id);
        Assert.NotNull(recipe);
        Assert.Single(recipe!.Lines);
        Assert.Equal(beerId, recipe.Lines[0].IngredientId);

        var list = await sut.ListAsync(new ListQuery { Page = 1, PageSize = 100 });
        Assert.Equal(2.5m, list.Items.Single(p => p.Id == created.Id).CostPrice);
    }

    [Fact]
    public async Task CreateAsync_resale_without_ingredient_throws()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Drinks", SortOrder = 0, IsActive = true });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(
                new CreateProductDto
                {
                    ProductTypeId = typeId,
                    Name = "Beer pint",
                    CompositionType = EProductType.Resale,
                    UnitPrice = 6m,
                }));
    }

    [Fact]
    public async Task CreateAsync_persists_composition_type()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", SortOrder = 0, IsActive = true });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            new CreateProductDto
            {
                ProductTypeId = typeId,
                Name = "Burger",
                CompositionType = EProductType.Prepared,
                UnitPrice = 5m,
            });

        Assert.Equal(EProductType.Prepared, created.CompositionType);
    }

    [Fact]
    public async Task SetRecipeAsync_throws_when_recipe_is_empty()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", SortOrder = 0, IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Cake",
                CompositionType = EProductType.Prepared,
                UnitPrice = 20m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetRecipeAsync(productId, new SetProductRecipeDto { Lines = [] }));
    }

    [Fact]
    public async Task SetRecipeAsync_resale_requires_exactly_one_ingredient_with_quantity_one()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var beerId = Guid.NewGuid();

        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Drinks", SortOrder = 0, IsActive = true });
        fx.Db.IngredientCategories.Add(
            new IngredientCategory { Id = categoryId, TenantId = fx.TenantId, Name = "Beverages", SortOrder = 0, IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Beer pint",
                CompositionType = EProductType.Resale,
                UnitPrice = 6m,
                IsActive = true,
            });
        fx.Db.Ingredients.Add(
            new Ingredient
            {
                Id = beerId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Beer",
                Unit = IngredientUnit.Unit,
                UnitCost = 2.5m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);

        var recipe = await sut.SetRecipeAsync(
            productId,
            new SetProductRecipeDto
            {
                Lines = [new SetProductRecipeLineDto { IngredientId = beerId, Quantity = 1m }],
            });

        Assert.NotNull(recipe);
        Assert.Equal(EProductType.Resale, recipe!.CompositionType);
        Assert.Single(recipe.Lines);
        Assert.Equal(2.5m, recipe.CostPrice);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetRecipeAsync(
                productId,
                new SetProductRecipeDto
                {
                    Lines =
                    [
                        new SetProductRecipeLineDto { IngredientId = beerId, Quantity = 2m },
                    ],
                }));

        var flourId = Guid.NewGuid();
        fx.Db.Ingredients.Add(
            new Ingredient
            {
                Id = flourId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Flour",
                Unit = IngredientUnit.Kilogram,
                UnitCost = 1m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SetRecipeAsync(
                productId,
                new SetProductRecipeDto
                {
                    Lines =
                    [
                        new SetProductRecipeLineDto { IngredientId = beerId, Quantity = 1m },
                        new SetProductRecipeLineDto { IngredientId = flourId, Quantity = 1m },
                    ],
                }));
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

    [Fact]
    public async Task SetImageAsync_saves_path_and_returns_image_url()
    {
        using var fx = new TenantDbFixture();
        var images = new FakeProductImageStorage();
        var typeId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", SortOrder = 0, IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Burger",
                UnitPrice = 12m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx, images);
        await using var stream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xD9]);
        var updated = await sut.SetImageAsync(productId, stream, "burger.jpg");

        Assert.NotNull(updated);
        Assert.NotNull(updated!.ImagePath);
        Assert.Contains(productId.ToString("N"), updated.ImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(updated.ImageUrl);
        Assert.True(images.FileExists(updated.ImagePath));
    }

    [Fact]
    public async Task SetImageAsync_replaces_previous_image_file()
    {
        using var fx = new TenantDbFixture();
        var images = new FakeProductImageStorage();
        var typeId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", SortOrder = 0, IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Burger",
                UnitPrice = 12m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx, images);
        await using var first = new MemoryStream([1, 2, 3]);
        var firstResult = await sut.SetImageAsync(productId, first, "a.jpg");
        var firstPath = firstResult!.ImagePath!;

        await using var second = new MemoryStream([4, 5, 6]);
        var secondResult = await sut.SetImageAsync(productId, second, "b.png");

        Assert.NotEqual(firstPath, secondResult!.ImagePath);
        Assert.False(images.FileExists(firstPath));
        Assert.True(images.FileExists(secondResult.ImagePath));
    }

    [Fact]
    public async Task RemoveImageAsync_clears_path_and_deletes_file()
    {
        using var fx = new TenantDbFixture();
        var images = new FakeProductImageStorage();
        var typeId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        fx.Db.ProductTypes.Add(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", SortOrder = 0, IsActive = true });
        fx.Db.Products.Add(
            new Product
            {
                Id = productId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                Name = "Burger",
                UnitPrice = 12m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx, images);
        await using var stream = new MemoryStream([9, 9, 9]);
        var withImage = await sut.SetImageAsync(productId, stream, "burger.jpg");
        var path = withImage!.ImagePath!;

        var removed = await sut.RemoveImageAsync(productId);

        Assert.NotNull(removed);
        Assert.Null(removed!.ImagePath);
        Assert.Null(removed.ImageUrl);
        Assert.False(images.FileExists(path));
    }

    [Fact]
    public async Task CreateAsync_bundle_links_component_products_and_costs_roll_up()
    {
        using var fx = new TenantDbFixture();
        var typeId = Guid.NewGuid();
        var promoTypeId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var burgerId = Guid.NewGuid();
        var drinkId = Guid.NewGuid();
        var bunIngredientId = Guid.NewGuid();
        var drinkIngredientId = Guid.NewGuid();

        fx.Db.ProductTypes.AddRange(
            new ProductType { Id = typeId, TenantId = fx.TenantId, Name = "Food", SortOrder = 0, IsActive = true },
            new ProductType { Id = promoTypeId, TenantId = fx.TenantId, Name = "Promociones", SortOrder = 1, IsActive = true });
        fx.Db.IngredientCategories.Add(
            new IngredientCategory { Id = categoryId, TenantId = fx.TenantId, Name = "Dry", SortOrder = 0, IsActive = true });
        fx.Db.Ingredients.AddRange(
            new Ingredient
            {
                Id = bunIngredientId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Bun",
                Unit = IngredientUnit.Unit,
                UnitCost = 2m,
                IsActive = true,
            },
            new Ingredient
            {
                Id = drinkIngredientId,
                TenantId = fx.TenantId,
                IngredientCategoryId = categoryId,
                Name = "Cola bottle",
                Unit = IngredientUnit.Unit,
                UnitCost = 1.5m,
                IsActive = true,
            });
        fx.Db.Products.AddRange(
            new Product
            {
                Id = burgerId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                CompositionType = EProductType.Prepared,
                Name = "Burger",
                UnitPrice = 10m,
                IsActive = true,
            },
            new Product
            {
                Id = drinkId,
                TenantId = fx.TenantId,
                ProductTypeId = typeId,
                CompositionType = EProductType.Resale,
                Name = "Cola",
                UnitPrice = 3m,
                IsActive = true,
            });
        fx.Db.ProductIngredients.AddRange(
            new ProductIngredient
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductId = burgerId,
                IngredientId = bunIngredientId,
                Quantity = 1m,
            },
            new ProductIngredient
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProductId = drinkId,
                IngredientId = drinkIngredientId,
                Quantity = 1m,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            new CreateProductDto
            {
                ProductTypeId = promoTypeId,
                CompositionType = EProductType.Bundle,
                Name = "Combo burger + cola",
                UnitPrice = 11m,
                BundleLines =
                [
                    new SetProductBundleLineDto { ComponentProductId = burgerId, Quantity = 1m },
                    new SetProductBundleLineDto { ComponentProductId = drinkId, Quantity = 1m },
                ],
            });

        Assert.Equal(EProductType.Bundle, created.CompositionType);
        Assert.Equal(3.5m, created.CostPrice);

        var bundle = await sut.GetBundleAsync(created.Id);
        Assert.NotNull(bundle);
        Assert.Equal(2, bundle!.Lines.Count);
    }
}
