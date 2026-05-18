using Microsoft.EntityFrameworkCore;
using Restaurant.Application.Features.Procurement.Purchases;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;
using Restaurant.Tests.Unit.Support;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class PurchaseServiceTests
{
    [Fact]
    public async Task CreateAsync_updates_stock_and_weighted_average_cost()
    {
        using var fx = new TenantDbFixture();
        var providerId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        fx.Db.IngredientCategories.Add(
            new IngredientCategory
            {
                Id = categoryId,
                TenantId = fx.TenantId,
                Name = "Dry",
                SortOrder = 0,
                IsActive = true,
            });
        fx.Db.Providers.Add(
            new Provider
            {
                Id = providerId,
                TenantId = fx.TenantId,
                Name = "Supplier A",
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
                StockQuantity = 10m,
                UnitCost = 2m,
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        var purchasedAt = DateTime.UtcNow;
        var created = await sut.CreateAsync(
            new CreatePurchaseDto
            {
                ProviderId = providerId,
                BillNumber = "INV-100",
                PurchasedAtUtc = purchasedAt,
                TaxAmount = 5m,
                Lines =
                [
                    new CreatePurchaseLineDto
                    {
                        IngredientId = ingredientId,
                        Quantity = 10m,
                        UnitPrice = 4m,
                    },
                ],
            });

        Assert.Equal("INV-100", created.BillNumber);
        Assert.Equal(40m, created.Subtotal);
        Assert.Equal(5m, created.TaxAmount);
        Assert.Equal(45m, created.Total);

        var ingredient = await fx.Db.Ingredients.FindAsync(ingredientId);
        Assert.Equal(20m, ingredient!.StockQuantity);
        Assert.Equal(3m, ingredient.UnitCost);
    }

    [Fact]
    public async Task CreateAsync_throws_when_duplicate_ingredient_lines()
    {
        using var fx = new TenantDbFixture();
        var providerId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        fx.Db.IngredientCategories.Add(
            new IngredientCategory
            {
                Id = categoryId,
                TenantId = fx.TenantId,
                Name = "Dry",
                SortOrder = 0,
                IsActive = true,
            });
        fx.Db.Providers.Add(
            new Provider
            {
                Id = providerId,
                TenantId = fx.TenantId,
                Name = "Supplier A",
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
                IsActive = true,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(
                new CreatePurchaseDto
                {
                    ProviderId = providerId,
                    BillNumber = "INV-200",
                    PurchasedAtUtc = DateTime.UtcNow,
                    Lines =
                    [
                        new CreatePurchaseLineDto { IngredientId = ingredientId, Quantity = 1m, UnitPrice = 1m },
                        new CreatePurchaseLineDto { IngredientId = ingredientId, Quantity = 2m, UnitPrice = 2m },
                    ],
                }));
    }

    [Fact]
    public async Task CreateAsync_throws_when_bill_number_already_exists()
    {
        using var fx = new TenantDbFixture();
        var providerId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        fx.Db.IngredientCategories.Add(
            new IngredientCategory
            {
                Id = categoryId,
                TenantId = fx.TenantId,
                Name = "Dry",
                SortOrder = 0,
                IsActive = true,
            });
        fx.Db.Providers.Add(
            new Provider
            {
                Id = providerId,
                TenantId = fx.TenantId,
                Name = "Supplier A",
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
                IsActive = true,
            });
        fx.Db.Purchases.Add(
            new Purchase
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProviderId = providerId,
                BillNumber = "INV-DUP",
                PurchasedAtUtc = DateTime.UtcNow,
                Subtotal = 1m,
                TaxAmount = 0m,
                Total = 1m,
            });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(
                new CreatePurchaseDto
                {
                    ProviderId = providerId,
                    BillNumber = "INV-DUP",
                    PurchasedAtUtc = DateTime.UtcNow,
                    Lines =
                    [
                        new CreatePurchaseLineDto { IngredientId = ingredientId, Quantity = 1m, UnitPrice = 1m },
                    ],
                }));
    }

    private static PurchaseService CreateSut(TenantDbFixture fx) =>
        new(
            fx.Repository<Purchase>(),
            fx.Repository<PurchaseLine>(),
            fx.Repository<Provider>(),
            fx.Repository<Ingredient>(),
            fx.UnitOfWork,
            fx.Mapper);
}
