using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Catalog.Ingredients;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class IngredientServiceTests
{
    private static IngredientService CreateSut(TenantDbFixture fx) =>
        new(fx.Repository<Ingredient>(), fx.UnitOfWork, fx.Mapper);

    [Fact]
    public async Task CreateAsync_persists_ingredient_with_tenant_id()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);

        var dto = await sut.CreateAsync(
            new CreateIngredientDto
            {
                Name = "  Flour  ",
                Unit = IngredientUnit.Kilogram,
                StockQuantity = 10m,
                ReorderLevel = 2m,
            });

        Assert.False(string.IsNullOrEmpty(dto.Name));
        Assert.Equal("Flour", dto.Name);
        var stored = await fx.Db.Ingredients.AsNoTracking().SingleAsync();
        Assert.Equal(fx.TenantId, stored.TenantId);
        Assert.Equal(IngredientUnit.Kilogram, stored.Unit);
    }

    [Fact]
    public async Task CreateAsync_throws_when_active_duplicate_name()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);
        await sut.CreateAsync(
            new CreateIngredientDto { Name = "Salt", Unit = IngredientUnit.Gram },
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(new CreateIngredientDto { Name = "Salt", Unit = IngredientUnit.Gram }));
    }

    [Fact]
    public async Task CreateAsync_allows_same_name_when_previous_is_inactive()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);
        var first = await sut.CreateAsync(
            new CreateIngredientDto { Name = "Pepper", Unit = IngredientUnit.Gram });
        await sut.SoftDeleteAsync(first.Id);

        var second = await sut.CreateAsync(
            new CreateIngredientDto { Name = "Pepper", Unit = IngredientUnit.Gram });

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(second.IsActive);
    }

    [Fact]
    public async Task ListAsync_excludes_inactive_by_default()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);
        var a = await sut.CreateAsync(new CreateIngredientDto { Name = "A", Unit = IngredientUnit.Unit });
        await sut.SoftDeleteAsync(a.Id);
        await sut.CreateAsync(new CreateIngredientDto { Name = "B", Unit = IngredientUnit.Unit });

        var list = await sut.ListAsync(includeInactive: false);

        Assert.Single(list);
        Assert.Equal("B", list[0].Name);
    }

    [Fact]
    public async Task ListAsync_includeInactive_returns_all()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);
        var a = await sut.CreateAsync(new CreateIngredientDto { Name = "X", Unit = IngredientUnit.Unit });
        await sut.SoftDeleteAsync(a.Id);
        await sut.CreateAsync(new CreateIngredientDto { Name = "Y", Unit = IngredientUnit.Unit });

        var list = await sut.ListAsync(includeInactive: true);

        Assert.Equal(2, list.Count);
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
        var sut = CreateSut(fx);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            new UpdateIngredientDto
            {
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
        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            new CreateIngredientDto { Name = "Sugar", Unit = IngredientUnit.Gram });

        var updated = await sut.UpdateAsync(
            created.Id,
            new UpdateIngredientDto
            {
                Name = "Brown sugar",
                Unit = IngredientUnit.Kilogram,
                StockQuantity = 5m,
                ReorderLevel = 1m,
                IsActive = true,
            });

        Assert.NotNull(updated);
        Assert.Equal("Brown sugar", updated!.Name);
        Assert.Equal(IngredientUnit.Kilogram, updated.Unit);
        Assert.Equal(5m, updated.StockQuantity);
    }

    [Fact]
    public async Task SoftDeleteAsync_returns_false_when_already_inactive()
    {
        using var fx = new TenantDbFixture();
        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            new CreateIngredientDto { Name = "Herb", Unit = IngredientUnit.Unit });
        await sut.SoftDeleteAsync(created.Id);

        var second = await sut.SoftDeleteAsync(created.Id);

        Assert.False(second);
    }
}
