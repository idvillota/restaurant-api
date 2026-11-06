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
        Assert.Equal(purchasedAt, created.PurchasedAtUtc);
        Assert.Equal(purchasedAt, created.PaymentDateUtc);

        var ingredient = await fx.Db.Ingredients.FindAsync(ingredientId);
        Assert.Equal(20m, ingredient!.StockQuantity);
        Assert.Equal(3m, ingredient.UnitCost);
    }

    [Fact]
    public async Task CreateAsync_when_inventory_disabled_persists_purchase_without_stock_change()
    {
        using var fx = new TenantDbFixture();
        var providerId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        fx.Db.TenantSettings.Add(
            new TenantSettings { TenantId = fx.TenantId, InventoryEnabled = false });
        fx.Db.IngredientCategories.Add(
            new IngredientCategory
            {
                Id = categoryId,
                TenantId = fx.TenantId,
                Name = "Dry",
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
        var created = await sut.CreateAsync(
            new CreatePurchaseDto
            {
                ProviderId = providerId,
                BillNumber = "INV-NO-STOCK",
                PurchasedAtUtc = DateTime.UtcNow,
                TaxAmount = 0m,
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

        Assert.Equal("INV-NO-STOCK", created.BillNumber);
        Assert.Equal(40m, created.Total);

        var ingredient = await fx.Db.Ingredients.FindAsync(ingredientId);
        Assert.Equal(10m, ingredient!.StockQuantity);
        Assert.Equal(2m, ingredient.UnitCost);
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
        var dupAt = DateTime.UtcNow;
        fx.Db.Purchases.Add(
            new Purchase
            {
                Id = Guid.NewGuid(),
                TenantId = fx.TenantId,
                ProviderId = providerId,
                BillNumber = "INV-DUP",
                PurchasedAtUtc = dupAt,
                PaymentDateUtc = dupAt,
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

    [Fact]
    public async Task CreateAsync_uses_distinct_payment_date_when_provided()
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

        var purchasedAt = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var paymentAt = new DateTime(2026, 2, 15, 9, 0, 0, DateTimeKind.Utc);
        var sut = CreateSut(fx);

        var created = await sut.CreateAsync(
            new CreatePurchaseDto
            {
                ProviderId = providerId,
                BillNumber = "CREDIT-01",
                PurchasedAtUtc = purchasedAt,
                PaymentDateUtc = paymentAt,
                Lines =
                [
                    new CreatePurchaseLineDto { IngredientId = ingredientId, Quantity = 1m, UnitPrice = 10m },
                ],
            });

        Assert.Equal(purchasedAt, created.PurchasedAtUtc);
        Assert.Equal(paymentAt, created.PaymentDateUtc);
    }

    [Fact]
    public async Task UpdatePaymentDateAsync_updates_only_payment_date()
    {
        using var fx = new TenantDbFixture();
        var providerId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();

        fx.Db.IngredientCategories.Add(
            new IngredientCategory
            {
                Id = categoryId,
                TenantId = fx.TenantId,
                Name = "Dry",
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
        var purchasedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var originalPayment = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc);
        fx.Db.Purchases.Add(
            new Purchase
            {
                Id = purchaseId,
                TenantId = fx.TenantId,
                ProviderId = providerId,
                BillNumber = "INV-300",
                PurchasedAtUtc = purchasedAt,
                PaymentDateUtc = originalPayment,
                Subtotal = 10m,
                TaxAmount = 0m,
                Total = 10m,
            });
        await fx.Db.SaveChangesAsync();

        var newPayment = new DateTime(2026, 3, 1, 14, 30, 0, DateTimeKind.Utc);
        var sut = CreateSut(fx);
        var updated = await sut.UpdatePaymentDateAsync(
            purchaseId,
            new UpdatePurchasePaymentDateDto { PaymentDateUtc = newPayment });

        Assert.NotNull(updated);
        Assert.Equal(purchasedAt, updated!.PurchasedAtUtc);
        Assert.Equal(newPayment, updated.PaymentDateUtc);
    }

    [Fact]
    public async Task CreateAsync_throws_when_bill_number_blank_and_auto_generate_off()
    {
        using var fx = new TenantDbFixture();
        var (providerId, ingredientId) = await SeedPurchaseGraphAsync(fx);

        var sut = CreateSut(fx);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAsync(MinimalCreateDto(providerId, ingredientId, billNumber: "  ")));

        Assert.Equal("Bill number is required.", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_generates_compra_number_when_blank_and_flag_on()
    {
        using var fx = new TenantDbFixture();
        var (providerId, ingredientId) = await SeedPurchaseGraphAsync(fx);
        fx.Db.TenantSettings.Add(
            new TenantSettings { TenantId = fx.TenantId, AutoGeneratePurchaseBillNumbers = true });
        await fx.Db.SaveChangesAsync();

        var purchasedAt = new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc);
        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            MinimalCreateDto(providerId, ingredientId, billNumber: null, purchasedAt));

        Assert.Equal("COMPRA-20261103-0001", created.BillNumber);
    }

    [Fact]
    public async Task CreateAsync_increments_generated_compra_number_same_day()
    {
        using var fx = new TenantDbFixture();
        var (providerId, ingredientId) = await SeedPurchaseGraphAsync(fx);
        fx.Db.TenantSettings.Add(
            new TenantSettings { TenantId = fx.TenantId, AutoGeneratePurchaseBillNumbers = true });
        await fx.Db.SaveChangesAsync();

        var purchasedAt = new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc);
        var sut = CreateSut(fx);
        var first = await sut.CreateAsync(
            MinimalCreateDto(providerId, ingredientId, billNumber: null, purchasedAt));
        var second = await sut.CreateAsync(
            MinimalCreateDto(providerId, ingredientId, billNumber: "", purchasedAt));

        Assert.Equal("COMPRA-20261103-0001", first.BillNumber);
        Assert.Equal("COMPRA-20261103-0002", second.BillNumber);
    }

    [Fact]
    public async Task CreateAsync_keeps_typed_bill_number_when_auto_generate_on()
    {
        using var fx = new TenantDbFixture();
        var (providerId, ingredientId) = await SeedPurchaseGraphAsync(fx);
        fx.Db.TenantSettings.Add(
            new TenantSettings { TenantId = fx.TenantId, AutoGeneratePurchaseBillNumbers = true });
        await fx.Db.SaveChangesAsync();

        var sut = CreateSut(fx);
        var created = await sut.CreateAsync(
            MinimalCreateDto(providerId, ingredientId, billNumber: "FAC-99"));

        Assert.Equal("FAC-99", created.BillNumber);
    }

    private static CreatePurchaseDto MinimalCreateDto(
        Guid providerId,
        Guid ingredientId,
        string? billNumber,
        DateTime? purchasedAt = null) =>
        new()
        {
            ProviderId = providerId,
            BillNumber = billNumber,
            PurchasedAtUtc = purchasedAt ?? DateTime.UtcNow,
            Lines =
            [
                new CreatePurchaseLineDto { IngredientId = ingredientId, Quantity = 1m, UnitPrice = 1m },
            ],
        };

    private static async Task<(Guid ProviderId, Guid IngredientId)> SeedPurchaseGraphAsync(TenantDbFixture fx)
    {
        var providerId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        fx.Db.IngredientCategories.Add(
            new IngredientCategory
            {
                Id = categoryId,
                TenantId = fx.TenantId,
                Name = "Dry",
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
        return (providerId, ingredientId);
    }

    private static PurchaseService CreateSut(TenantDbFixture fx) =>
        new(
            fx.Repository<Purchase>(),
            fx.Repository<PurchaseLine>(),
            fx.Repository<Provider>(),
            fx.Repository<Ingredient>(),
            fx.Db,
            fx.UnitOfWork,
            fx.Mapper);
}
