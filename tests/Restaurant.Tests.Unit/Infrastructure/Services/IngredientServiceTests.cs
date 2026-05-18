using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Common.Models;
using Restaurant.Application.Features.Catalog.Ingredients;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class IngredientServiceTests
{
    private static IngredientService CreateSut(TenantDbFixture fx) =>
        new(fx.Repository<Ingredient>(), fx.Repository<IngredientCategory>(), fx.UnitOfWork, fx.Mapper);

    private static async Task<Guid> SeedCategoryAsync(TenantDbFixture fx)
    {
        var cat = new IngredientCategory
        {
            Id = Guid.NewGuid(),
            TenantId = fx.TenantId,
            Name = "General",
            SortOrder = 0,
            IsActive = true,
        };
        await fx.Db.IngredientCategories.AddAsync(cat);
        await fx.Db.SaveChangesAsync();
        return cat.Id;
    }

    [Fact]
    public async Task CreateAsync_persists_ingredient_with_tenant_id()
    {
        using var fx = new TenantDbFixture();
        var catId = await SeedCategoryAsync(fx);
        var sut = CreateSut(fx);

        var dto = await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "  Flour  ",
                Unit = IngredientUnit.Kilogram,
                ReorderLevel = 2m,
            });

        Assert.False(string.IsNullOrEmpty(dto.Name));
        Assert.Equal("Flour", dto.Name);
        Assert.Equal(catId, dto.IngredientCategoryId);
        var stored = await fx.Db.Ingredients.AsNoTracking().SingleAsync();
        Assert.Equal(fx.TenantId, stored.TenantId);
        Assert.Equal(IngredientUnit.Kilogram, stored.Unit);
        Assert.Null(stored.UnitCost);
    }

    [Fact]
    public async Task CreateAsync_persists_optional_initial_unit_cost()
    {
        using var fx = new TenantDbFixture();
        var catId = await SeedCategoryAsync(fx);
        var sut = CreateSut(fx);

        var dto = await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "Canela",
                Unit = IngredientUnit.Gram,
                UnitCost = 12.5m,
            });

        Assert.Equal(12.5m, dto.UnitCost);
        var stored = await fx.Db.Ingredients.AsNoTracking().SingleAsync();
        Assert.Equal(12.5m, stored.UnitCost);
    }

    [Fact]
    public async Task CreateAsync_throws_when_active_duplicate_name()
    {
        using var fx = new TenantDbFixture();
        var catId = await SeedCategoryAsync(fx);
        var sut = CreateSut(fx);
        await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "Salt",
                Unit = IngredientUnit.Gram,
            },
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(
                new CreateIngredientDto
                {
                    IngredientCategoryId = catId,
                    Name = "Salt",
                    Unit = IngredientUnit.Gram,
                }));
    }

    [Fact]
    public async Task CreateAsync_allows_same_name_when_previous_is_inactive()
    {
        using var fx = new TenantDbFixture();
        var catId = await SeedCategoryAsync(fx);
        var sut = CreateSut(fx);
        var first = await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "Pepper",
                Unit = IngredientUnit.Gram,
            });
        await sut.SoftDeleteAsync(first.Id);

        var second = await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "Pepper",
                Unit = IngredientUnit.Gram,
            });

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(second.IsActive);
    }

    [Fact]
    public async Task ListAsync_excludes_inactive_by_default()
    {
        using var fx = new TenantDbFixture();
        var catId = await SeedCategoryAsync(fx);
        var sut = CreateSut(fx);
        var a = await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "A",
                Unit = IngredientUnit.Unit,
            });
        await sut.SoftDeleteAsync(a.Id);
        await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "B",
                Unit = IngredientUnit.Unit,
            });

        var list = await sut.ListAsync(new ListQuery { Page = 1, PageSize = 100 });

        Assert.Single(list.Items);
        Assert.Equal("B", list.Items[0].Name);
    }

    [Fact]
    public async Task ListAsync_includeInactive_returns_all()
    {
        using var fx = new TenantDbFixture();
        var catId = await SeedCategoryAsync(fx);
        var sut = CreateSut(fx);
        var a = await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "X",
                Unit = IngredientUnit.Unit,
            });
        await sut.SoftDeleteAsync(a.Id);
        await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "Y",
                Unit = IngredientUnit.Unit,
            });

        var list = await sut.ListAsync(new ListQuery { Page = 1, PageSize = 100, IncludeInactive = true });

        Assert.Equal(2, list.TotalCount);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_missing()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_when_missing()
    {
        using var fx = new TenantDbFixture();
        var catId = await SeedCategoryAsync(fx);
        var sut = CreateSut(fx);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            new UpdateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "N",
                Unit = IngredientUnit.Unit,
                IsActive = true,
            });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_updates_fields()
    {
        using var fx = new TenantDbFixture();
        var catId = await SeedCategoryAsync(fx);
        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "Sugar",
                Unit = IngredientUnit.Gram,
            });

        var updated = await sut.UpdateAsync(
            created.Id,
            new UpdateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "Brown sugar",
                Unit = IngredientUnit.Kilogram,
                ReorderLevel = 1m,
                IsActive = true,
            });

        Assert.NotNull(updated);
        Assert.Equal("Brown sugar", updated!.Name);
        Assert.Equal(IngredientUnit.Kilogram, updated.Unit);
        Assert.Equal(1m, updated.ReorderLevel);

        var stored = await fx.Db.Ingredients.AsNoTracking().SingleAsync(i => i.Id == created.Id);
        Assert.Null(stored.StockQuantity);
    }

    [Fact]
    public async Task SoftDeleteAsync_returns_false_when_already_inactive()
    {
        using var fx = new TenantDbFixture();
        var catId = await SeedCategoryAsync(fx);
        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            new CreateIngredientDto
            {
                IngredientCategoryId = catId,
                Name = "Herb",
                Unit = IngredientUnit.Unit,
            });
        await sut.SoftDeleteAsync(created.Id);

        var second = await sut.SoftDeleteAsync(created.Id);

        Assert.False(second);
    }
}
