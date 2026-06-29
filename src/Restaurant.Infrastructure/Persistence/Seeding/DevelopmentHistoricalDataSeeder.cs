using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Domain.Common;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Services;

namespace Restaurant.Infrastructure.Persistence.Seeding;

/// <summary>
/// Extends the demo tenant with more catalog items and ~6 months of purchases, sales, bills, and payments.
/// Idempotent: skips when historical orders (prefix HIST-) already exist.
/// </summary>
public static class DevelopmentHistoricalDataSeeder
{
    public const string HistoricalOrderNumberPrefix = "HIST-";

    private const int HistoryDays = 183;
    private const int SeedSalt = 20260601;
    private const int BatchSaveEveryDays = 14;

    private static Guid ExtId(string familyHex, int index) =>
        Guid.Parse($"{familyHex}-{index:0004}-4001-8001-{index:D12}");

    public static async Task SeedAsync(
        ApplicationDbContext db,
        ILogger logger,
        ICurrentTenantContext? tenantContext = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.Slug == DevelopmentSeedIds.TenantSlug, cancellationToken);
        if (tenant is null)
            return;

        Guid? previousTenantId = tenantContext?.TenantId;
        if (tenantContext is not null)
            tenantContext.TenantId = tenant.Id;

        try
        {
            await SeedCoreAsync(db, tenant.Id, logger, cancellationToken);
        }
        finally
        {
            if (tenantContext is not null)
                tenantContext.TenantId = previousTenantId;
        }
    }

    private static async Task SeedCoreAsync(
        ApplicationDbContext db,
        Guid tenantId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (await db.SalesOrders.IgnoreQueryFilters()
                .AnyAsync(
                    s => s.TenantId == tenantId && s.Number.StartsWith(HistoricalOrderNumberPrefix),
                    cancellationToken))
        {
            logger.LogInformation(
                "Historical development seed skipped: data already exists for '{Slug}'.",
                DevelopmentSeedIds.TenantSlug);
            return;
        }

        var utcNow = DateTime.UtcNow;
        var rng = new Random(SeedSalt);

        var catalog = await ExtendCatalogAsync(db, tenantId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var context = await LoadOperationalContextAsync(db, tenantId, catalog.AllProductIds, cancellationToken);
        if (context.Customers.Count == 0 || context.Providers.Count == 0)
        {
            logger.LogWarning("Historical seed aborted: missing customers or providers for demo tenant.");
            return;
        }

        var settings = await db.TenantSettings.IgnoreQueryFilters()
            .SingleOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        var impoconsumoPercent = settings?.ImpoconsumoPercent ?? 8m;

        var startDay = DateOnly.FromDateTime(utcNow.Date).AddDays(-HistoryDays);
        var dianConsecutive = settings?.DianNextConsecutive ?? 1000;
        var orderSeq = 1;
        var purchaseSeq = 1;
        var billSeq = 1;
        var daysSinceBatch = 0;

        var timestampPatches = new List<(EntityBase Entity, DateTime CreatedAtUtc)>();
        var shiftsByDate = new Dictionary<DateOnly, CashierShift>();

        logger.LogInformation(
            "Generating ~{Days} days of historical demo data for '{Slug}'…",
            HistoryDays,
            DevelopmentSeedIds.TenantSlug);

        for (var dayOffset = 0; dayOffset < HistoryDays; dayOffset++)
        {
            var businessDate = startDay.AddDays(dayOffset);
            var isWeekend = businessDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var dayUtc = DateTime.SpecifyKind(businessDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

            if (!shiftsByDate.ContainsKey(businessDate))
            {
                var shift = CreateCashierShift(tenantId, businessDate, dayUtc, rng);
                await db.CashierShifts.AddAsync(shift, cancellationToken);
                shiftsByDate[businessDate] = shift;
            }

            var shiftForDay = shiftsByDate[businessDate];

            if (dayOffset % 3 == 0 || (isWeekend && dayOffset % 2 == 0))
            {
                var purchase = CreatePurchase(
                    tenantId,
                    context,
                    rng,
                    purchaseSeq++,
                    dayUtc.AddHours(rng.Next(8, 11)),
                    timestampPatches);
                await db.Purchases.AddAsync(purchase.Purchase, cancellationToken);
                await db.PurchaseLines.AddRangeAsync(purchase.Lines, cancellationToken);
                ApplyPurchaseStock(purchase.Lines, context.IngredientsById);
            }

            var ordersToday = isWeekend ? rng.Next(14, 24) : rng.Next(9, 17);
            for (var o = 0; o < ordersToday; o++)
            {
                var soldAt = dayUtc.AddHours(PickRestaurantHour(rng, isWeekend)).AddMinutes(rng.Next(0, 59));
                var orderBundle = CreatePaidSale(
                    tenantId,
                    context,
                    catalog,
                    rng,
                    orderSeq++,
                    soldAt,
                    impoconsumoPercent,
                    shiftForDay,
                    ref billSeq,
                    ref dianConsecutive,
                    timestampPatches);

                await db.SalesOrders.AddAsync(orderBundle.Order, cancellationToken);
                await db.SalesOrderLines.AddRangeAsync(orderBundle.Lines, cancellationToken);
                await db.Invoices.AddAsync(orderBundle.Invoice, cancellationToken);
                await db.Payments.AddAsync(orderBundle.Payment, cancellationToken);

                if (orderBundle.Bill is not null)
                {
                    await db.Bills.AddAsync(orderBundle.Bill, cancellationToken);
                    await db.BillLines.AddRangeAsync(orderBundle.BillLines, cancellationToken);
                    await db.BillSalesOrders.AddAsync(orderBundle.BillOrderLink!, cancellationToken);
                    orderBundle.Payment.BillId = orderBundle.Bill.Id;
                    orderBundle.Invoice.BillId = orderBundle.Bill.Id;
                }
            }

            daysSinceBatch++;
            if (daysSinceBatch >= BatchSaveEveryDays || dayOffset == HistoryDays - 1)
            {
                await db.SaveChangesAsync(cancellationToken);
                await ApplyTimestampPatchesAsync(db, timestampPatches, cancellationToken);
                timestampPatches.Clear();
                daysSinceBatch = 0;
            }
        }

        if (settings is not null)
            settings.DianNextConsecutive = dianConsecutive + 1;

        await db.SaveChangesAsync(cancellationToken);

        var historicalOrders = await db.SalesOrders.IgnoreQueryFilters()
            .CountAsync(s => s.TenantId == tenantId && s.Number.StartsWith(HistoricalOrderNumberPrefix), cancellationToken);
        var historicalPurchases = await db.Purchases.IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tenantId && p.BillNumber.StartsWith("HIST-"), cancellationToken);

        logger.LogInformation(
            "Historical development seed completed for '{Slug}': {Orders} sales orders, {Purchases} purchases, {Products} products total.",
            DevelopmentSeedIds.TenantSlug,
            historicalOrders,
            historicalPurchases,
            catalog.AllProductIds.Count);
    }

    private static async Task ApplyTimestampPatchesAsync(
        ApplicationDbContext db,
        List<(EntityBase Entity, DateTime CreatedAtUtc)> patches,
        CancellationToken cancellationToken)
    {
        if (patches.Count == 0)
            return;

        foreach (var (entity, createdAt) in patches)
        {
            var entry = db.Entry(entity);
            if (entry.State == EntityState.Detached)
                continue;

            entry.Property(nameof(EntityBase.CreatedAtUtc)).CurrentValue = createdAt;
            entry.Property(nameof(EntityBase.UpdatedAtUtc)).CurrentValue = createdAt;
            entry.State = EntityState.Modified;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static int PickRestaurantHour(Random rng, bool isWeekend)
    {
        var lunchWeight = isWeekend ? 38 : 32;
        var dinnerWeight = isWeekend ? 42 : 38;
        var roll = rng.Next(100);

        if (roll < 6)
            return rng.Next(10, 12);
        if (roll < 6 + lunchWeight)
            return rng.Next(12, 15);
        if (roll < 6 + lunchWeight + 18)
            return rng.Next(15, 18);
        if (roll < 6 + lunchWeight + 18 + dinnerWeight)
            return rng.Next(18, 22);
        return rng.Next(22, 24);
    }

    private sealed record CatalogSnapshot(
        IReadOnlyList<Guid> AllProductIds,
        IReadOnlyDictionary<Guid, Product> ProductsById,
        IReadOnlyDictionary<Guid, string> ProductTypeNames);

    private sealed record OperationalContext(
        IReadOnlyList<Customer> Customers,
        IReadOnlyList<Provider> Providers,
        IReadOnlyList<DiningTable> Tables,
        IReadOnlyDictionary<Guid, Ingredient> IngredientsById,
        Guid CashierUserId);

    private static async Task<CatalogSnapshot> ExtendCatalogAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existingIngredients = await db.Ingredients.IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId)
            .ToDictionaryAsync(i => i.Name, i => i, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var newIngredients = new (string Name, IngredientUnit Unit, string CategoryName, decimal Reorder)[]
        {
            ("Pepperoni", IngredientUnit.Gram, "Carnes", 2500m),
            ("Queso parmesano", IngredientUnit.Gram, "Lácteos", 2000m),
            ("Lechuga romana", IngredientUnit.Gram, "Verduras", 3000m),
            ("Crutones", IngredientUnit.Gram, "Panadería", 1500m),
            ("Masa de pizza", IngredientUnit.Gram, "Panadería", 8000m),
            ("Cebolla morada", IngredientUnit.Gram, "Verduras", 4000m),
            ("Ajo", IngredientUnit.Gram, "Verduras", 800m),
            ("Limón", IngredientUnit.Unit, "Verduras", 40m),
            ("Café molido", IngredientUnit.Gram, "Despensa", 3000m),
            ("Azúcar", IngredientUnit.Gram, "Despensa", 5000m),
            ("Ron blanco", IngredientUnit.Milliliter, "Bebidas", 3000m),
            ("Menta fresca", IngredientUnit.Gram, "Verduras", 300m),
            ("Papa", IngredientUnit.Gram, "Verduras", 10000m),
            ("Tocineta", IngredientUnit.Gram, "Carnes", 3000m),
            ("Salsa BBQ", IngredientUnit.Milliliter, "Despensa", 2000m),
            ("Cerveza artesanal IPA 350 ml", IngredientUnit.Unit, "Bebidas", 24m),
            ("Vino tinto copa", IngredientUnit.Milliliter, "Bebidas", 5000m),
            ("Chocolate negro", IngredientUnit.Gram, "Despensa", 2000m),
            ("Champiñones", IngredientUnit.Gram, "Verduras", 3500m),
            ("Arroz arborio", IngredientUnit.Gram, "Despensa", 6000m),
            ("Huevo", IngredientUnit.Unit, "Lácteos", 60m),
        };

        var categories = await db.IngredientCategories.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var ingredientIndex = 1;
        foreach (var spec in newIngredients)
        {
            if (existingIngredients.ContainsKey(spec.Name))
                continue;

            if (!categories.TryGetValue(spec.CategoryName, out var categoryId))
                categoryId = categories.Values.First();

            var ingredient = new Ingredient
            {
                Id = ExtId("0c000001", ingredientIndex++),
                TenantId = tenantId,
                IngredientCategoryId = categoryId,
                Name = spec.Name,
                Unit = spec.Unit,
                ReorderLevel = spec.Reorder,
                StockQuantity = spec.Unit is IngredientUnit.Unit ? 60m : 15000m,
                UnitCost = spec.Unit is IngredientUnit.Unit ? 2500m : 8m,
                IsActive = true,
            };
            await db.Ingredients.AddAsync(ingredient, cancellationToken);
            existingIngredients[spec.Name] = ingredient;
        }

        var productTypes = await db.ProductTypes.IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId)
            .ToDictionaryAsync(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingProducts = await db.Products.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .ToDictionaryAsync(p => p.Sku ?? p.Name, p => p, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var newProducts = new (string Name, string TypeName, EProductType Kind, decimal Price, string Sku)[]
        {
            ("Pizza cuatro quesos", "Pizzas", EProductType.Prepared, 39000m, "PIZ-003"),
            ("Lasagna boloñesa", "Pastas", EProductType.Prepared, 34000m, "PAS-002"),
            ("Ensalada griega", "Ensaladas", EProductType.Prepared, 26000m, "SAL-002"),
            ("Hamburguesa BBQ", "Hamburguesas", EProductType.Prepared, 38000m, "BRG-002"),
            ("Brownie con helado", "Postres", EProductType.Prepared, 18000m, "DES-002"),
            ("Café americano", "Refrescos", EProductType.Prepared, 7000m, "DRK-004"),
            ("Mojito clásico", "Refrescos", EProductType.Prepared, 22000m, "DRK-005"),
            ("Cerveza artesanal IPA", "Cerveza", EProductType.Resale, 12000m, "BEER-001"),
            ("Copa de vino tinto", "Vino", EProductType.Prepared, 28000m, "WIN-001"),
            ("Nachos con queso", "Entradas", EProductType.Prepared, 22000m, "APP-002"),
            ("Alitas BBQ", "Entradas", EProductType.Prepared, 26000m, "APP-003"),
            ("Risotto de hongos", "Especiales", EProductType.Prepared, 44000m, "SPC-003"),
            ("Tarta de limón", "Postres", EProductType.Prepared, 16000m, "DES-003"),
            ("Pasta carbonara", "Pastas", EProductType.Prepared, 45000m, "PAS-003"),
            ("Combo pizza + cerveza", "Promociones", EProductType.Bundle, 42000m, "PRO-002"),
        };

        var productIndex = 1;
        Product? comboPizza = null;
        Product? comboBeer = null;

        foreach (var spec in newProducts)
        {
            if (existingProducts.ContainsKey(spec.Sku))
            {
                if (spec.Sku == "PIZ-003")
                    comboPizza = existingProducts[spec.Sku];
                if (spec.Sku == "BEER-001")
                    comboBeer = existingProducts[spec.Sku];
                continue;
            }

            if (!productTypes.TryGetValue(spec.TypeName, out var productType))
                productType = productTypes.Values.First();

            var product = new Product
            {
                Id = ExtId("0f000001", productIndex++),
                TenantId = tenantId,
                ProductTypeId = productType.Id,
                CompositionType = spec.Kind,
                Name = spec.Name,
                Description = $"Plato histórico demo — {spec.Name}",
                Sku = spec.Sku,
                UnitPrice = spec.Price,
                IsActive = true,
            };
            await db.Products.AddAsync(product, cancellationToken);
            existingProducts[spec.Sku] = product;

            if (spec.Sku == "PIZ-003")
                comboPizza = product;
            if (spec.Sku == "BEER-001")
                comboBeer = product;

            await AddRecipeLinesAsync(db, tenantId, product, spec.Sku, existingIngredients, productIndex, cancellationToken);
        }

        if (existingProducts.TryGetValue("PRO-002", out var combo) && comboPizza is not null && comboBeer is not null)
        {
            var hasBundle = await db.ProductBundleLines.IgnoreQueryFilters()
                .AnyAsync(b => b.TenantId == tenantId && b.ProductId == combo.Id, cancellationToken);
            if (!hasBundle)
            {
                await db.ProductBundleLines.AddRangeAsync(
                [
                    new ProductBundleLine
                    {
                        Id = ExtId("0b000001", 1),
                        TenantId = tenantId,
                        ProductId = combo.Id,
                        ComponentProductId = comboPizza.Id,
                        Quantity = 1m,
                        SortOrder = 0,
                    },
                    new ProductBundleLine
                    {
                        Id = ExtId("0b000001", 2),
                        TenantId = tenantId,
                        ProductId = combo.Id,
                        ComponentProductId = comboBeer.Id,
                        Quantity = 1m,
                        SortOrder = 1,
                    },
                ],
                cancellationToken);
            }
        }

        var allProducts = await db.Products.IgnoreQueryFilters()
            .Include(p => p.ProductType)
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .ToListAsync(cancellationToken);

        return new CatalogSnapshot(
            allProducts.Select(p => p.Id).ToList(),
            allProducts.ToDictionary(p => p.Id),
            allProducts.ToDictionary(p => p.Id, p => p.ProductType?.Name ?? "Otros"));
    }

    private static async Task AddRecipeLinesAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Product product,
        string sku,
        Dictionary<string, Ingredient> ingredients,
        int productIndex,
        CancellationToken cancellationToken)
    {
        Guid Ing(string name) => ingredients[name].Id;

        (string IngredientName, decimal Qty)[]? recipe = sku switch
        {
            "PIZ-003" => [("Masa de pizza", 280m), ("Mozzarella", 180m), ("Queso parmesano", 60m), ("Tomates", 120m)],
            "PAS-002" => [("Pasta penne", 200m), ("Carne molida", 150m), ("Tomates", 100m), ("Queso parmesano", 40m)],
            "SAL-002" => [("Lechuga romana", 120m), ("Tomates", 80m), ("Queso parmesano", 30m), ("Aceite de oliva", 15m)],
            "BRG-002" => [("Carne molida", 170m), ("Pan de hamburguesa", 1m), ("Tocineta", 40m), ("Salsa BBQ", 25m)],
            "DES-002" => [("Chocolate negro", 80m), ("Harina 00", 50m), ("Azúcar", 40m), ("Mozzarella", 30m)],
            "DRK-004" => [("Café molido", 18m), ("Azúcar", 5m)],
            "DRK-005" => [("Ron blanco", 60m), ("Menta fresca", 8m), ("Limón", 1m), ("Azúcar", 12m)],
            "BEER-001" => [("Cerveza artesanal IPA 350 ml", 1m)],
            "WIN-001" => [("Vino tinto copa", 150m)],
            "APP-002" => [("Harina 00", 90m), ("Mozzarella", 100m), ("Tomates", 60m)],
            "APP-003" => [("Pechuga de pollo", 220m), ("Salsa BBQ", 35m)],
            "SPC-003" => [("Arroz arborio", 180m), ("Champiñones", 120m), ("Queso parmesano", 35m)],
            "DES-003" => [("Harina 00", 70m), ("Limón", 2m), ("Azúcar", 55m)],
            "PAS-003" => [("Pasta penne", 190m), ("Tocineta", 60m), ("Queso parmesano", 45m), ("Huevo", 1m)],
            _ => null,
        };

        if (recipe is null)
            return;

        var recipeLineIndex = 1;
        foreach (var (ingredientName, qty) in recipe)
        {
            if (!ingredients.TryGetValue(ingredientName, out _))
                continue;

            await db.ProductIngredients.AddAsync(
                new ProductIngredient
                {
                    Id = ExtId("0a000001", productIndex * 10 + recipeLineIndex),
                    TenantId = tenantId,
                    ProductId = product.Id,
                    IngredientId = Ing(ingredientName),
                    Quantity = qty,
                },
                cancellationToken);
            recipeLineIndex++;
        }
    }

    private static async Task<OperationalContext> LoadOperationalContextAsync(
        ApplicationDbContext db,
        Guid tenantId,
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var customers = await db.Customers.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .ToListAsync(cancellationToken);

        var providers = await db.Providers.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .ToListAsync(cancellationToken);

        var tables = await db.DiningTables.IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .ToListAsync(cancellationToken);

        var ingredients = await db.Ingredients.IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.IsActive)
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        _ = productIds;

        return new OperationalContext(
            customers,
            providers,
            tables,
            ingredients,
            DevelopmentSeedIds.CashierUserId);
    }

    private static CashierShift CreateCashierShift(
        Guid tenantId,
        DateOnly businessDate,
        DateTime dayUtc,
        Random rng)
    {
        var opened = dayUtc.AddHours(9).AddMinutes(rng.Next(0, 30));
        var closed = dayUtc.AddHours(23).AddMinutes(rng.Next(0, 45));
        return new CashierShift
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashierUserId = DevelopmentSeedIds.CashierUserId,
            Status = CashierShiftStatus.Closed,
            BusinessDate = businessDate,
            OpenedAtUtc = opened,
            ClosedAtUtc = closed,
            OpeningFloat = 200_000m,
            ExpectedCash = rng.Next(800_000, 2_500_000),
            CountedCash = rng.Next(800_000, 2_500_000),
            ClosingNotes = "Turno histórico demo",
        };
    }

    private static (Purchase Purchase, List<PurchaseLine> Lines) CreatePurchase(
        Guid tenantId,
        OperationalContext context,
        Random rng,
        int sequence,
        DateTime purchasedAt,
        List<(EntityBase Entity, DateTime CreatedAtUtc)> timestampPatches)
    {
        var provider = context.Providers[rng.Next(context.Providers.Count)];
        var ingredientList = context.IngredientsById.Values.ToList();
        var lineCount = rng.Next(2, 5);
        var purchaseId = Guid.NewGuid();
        var lines = new List<PurchaseLine>();
        decimal subtotal = 0;

        var used = new HashSet<Guid>();
        for (var i = 0; i < lineCount; i++)
        {
            var ingredient = ingredientList[rng.Next(ingredientList.Count)];
            if (!used.Add(ingredient.Id))
                continue;

            var qty = ingredient.Unit switch
            {
                IngredientUnit.Unit => rng.Next(12, 72),
                IngredientUnit.Milliliter => rng.Next(2000, 12000),
                _ => rng.Next(3000, 25000),
            };
            var unitPrice = ingredient.Unit switch
            {
                IngredientUnit.Unit => rng.Next(1500, 4500),
                _ => rng.Next(4, 45),
            };
            var lineTotal = qty * unitPrice;
            subtotal += lineTotal;

            var line = new PurchaseLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PurchaseId = purchaseId,
                IngredientId = ingredient.Id,
                Quantity = qty,
                UnitPrice = unitPrice,
                LineTotal = lineTotal,
            };
            lines.Add(line);
            timestampPatches.Add((line, purchasedAt.AddMinutes(i + 1)));
        }

        subtotal = decimal.Round(subtotal, 0, MidpointRounding.AwayFromZero);
        var tax = decimal.Round(subtotal * 0.19m, 0, MidpointRounding.AwayFromZero);
        var paymentDate = rng.Next(100) < 55 ? purchasedAt : purchasedAt.AddDays(rng.Next(15, 45));

        var purchase = new Purchase
        {
            Id = purchaseId,
            TenantId = tenantId,
            ProviderId = provider.Id,
            BillNumber = $"HIST-P-{sequence:00000}",
            PurchasedAtUtc = purchasedAt,
            PaymentDateUtc = paymentDate,
            Subtotal = subtotal,
            TaxAmount = tax,
            Total = subtotal + tax,
            Notes = "Compra histórica demo",
        };
        timestampPatches.Add((purchase, purchasedAt));

        return (purchase, lines);
    }

    private static void ApplyPurchaseStock(
        IReadOnlyList<PurchaseLine> lines,
        IReadOnlyDictionary<Guid, Ingredient> ingredientsById)
    {
        foreach (var line in lines)
        {
            if (!ingredientsById.TryGetValue(line.IngredientId, out var ingredient))
                continue;

            ingredient.UnitCost = InventoryCosting.ComputeWeightedAverageUnitCost(
                ingredient.StockQuantity,
                ingredient.UnitCost,
                line.Quantity,
                line.UnitPrice);
            ingredient.StockQuantity = InventoryCosting.AddStock(ingredient.StockQuantity, line.Quantity);
        }
    }

    private sealed record PaidSaleBundle(
        SalesOrder Order,
        List<SalesOrderLine> Lines,
        Invoice Invoice,
        Payment Payment,
        Bill? Bill,
        List<BillLine> BillLines,
        BillSalesOrder? BillOrderLink);

    private static PaidSaleBundle CreatePaidSale(
        Guid tenantId,
        OperationalContext context,
        CatalogSnapshot catalog,
        Random rng,
        int sequence,
        DateTime soldAt,
        decimal impoconsumoPercent,
        CashierShift shift,
        ref int billSeq,
        ref int dianConsecutive,
        List<(EntityBase Entity, DateTime CreatedAtUtc)> timestampPatches)
    {
        var orderId = Guid.NewGuid();
        var customer = context.Customers[rng.Next(context.Customers.Count)];
        var table = context.Tables.Count > 0 ? context.Tables[rng.Next(context.Tables.Count)] : null;
        var lineCount = rng.Next(1, 5);
        var lines = new List<SalesOrderLine>();
        decimal subtotal = 0;

        var productCursor = (sequence + soldAt.Hour) % catalog.AllProductIds.Count;
        for (var l = 0; l < lineCount; l++)
        {
            var productId = catalog.AllProductIds[(productCursor + l * 3 + rng.Next(0, 2)) % catalog.AllProductIds.Count];
            var product = catalog.ProductsById[productId];
            var qty = product.CompositionType == EProductType.Bundle
                ? 1m
                : rng.Next(1, 4);
            var lineTotal = qty * product.UnitPrice;
            subtotal += lineTotal;

            var line = new SalesOrderLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SalesOrderId = orderId,
                ProductId = productId,
                Quantity = qty,
                UnitPrice = product.UnitPrice,
                LineTotal = lineTotal,
                SentToKitchenAtUtc = soldAt.AddMinutes(-rng.Next(8, 25)),
            };
            lines.Add(line);
            timestampPatches.Add((line, soldAt));
        }

        subtotal = decimal.Round(subtotal, 0, MidpointRounding.AwayFromZero);
        var divisor = 1m + (impoconsumoPercent / 100m);
        var tax = impoconsumoPercent > 0
            ? decimal.Round(subtotal - subtotal / divisor, 0, MidpointRounding.AwayFromZero)
            : 0m;
        var total = subtotal;

        var openedAt = soldAt.AddMinutes(-rng.Next(20, 55));
        var order = new SalesOrder
        {
            Id = orderId,
            TenantId = tenantId,
            DiningTableId = table?.Id,
            CustomerId = customer.Id,
            Number = $"{HistoricalOrderNumberPrefix}{sequence:000000}",
            OpenedAtUtc = openedAt,
            Status = SalesOrderStatus.Paid,
            Subtotal = subtotal,
            TaxAmount = tax,
            Total = total,
            ClosedAtUtc = soldAt,
        };
        timestampPatches.Add((order, openedAt));

        var invoiceId = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            SalesOrderId = orderId,
            CustomerId = customer.Id,
            Number = $"HIST-INV-{sequence:000000}",
            Status = InvoiceStatus.Paid,
            Subtotal = subtotal,
            TaxAmount = tax,
            Total = total,
            IssuedAtUtc = soldAt,
            DueAtUtc = soldAt.AddDays(14),
        };
        timestampPatches.Add((invoice, soldAt));

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SalesOrderId = orderId,
            InvoiceId = invoiceId,
            Amount = total,
            Method = ((sequence + soldAt.Hour) % 3) switch
            {
                0 => PaymentMethod.Cash,
                1 => PaymentMethod.Card,
                _ => PaymentMethod.Transfer,
            },
            Status = PaymentStatus.Completed,
            ExternalReference = $"HIST-PAY-{sequence:000000}",
            PaidAtUtc = soldAt,
            CashierShiftId = shift.Id,
            ProcessedByUserId = context.CashierUserId,
        };
        timestampPatches.Add((payment, soldAt));

        Bill? bill = null;
        List<BillLine> billLines = [];
        BillSalesOrder? billOrderLink = null;

        if (rng.Next(100) < 72)
        {
            var billId = Guid.NewGuid();
            dianConsecutive++;
            bill = new Bill
            {
                Id = billId,
                TenantId = tenantId,
                CustomerId = customer.Id,
                Number = $"HIST-BILL-{billSeq:000000}",
                Status = BillStatus.Paid,
                Subtotal = subtotal,
                DiscountAmount = 0,
                TipAmount = 0,
                TaxAmount = tax,
                Total = total,
                IssuedAtUtc = soldAt.AddMinutes(-3),
                PaidAtUtc = soldAt,
                CashierShiftId = shift.Id,
                ProcessedByUserId = context.CashierUserId,
                DianConsecutiveNumber = dianConsecutive,
                ProcessedByDisplayName = "Cajero demo",
                TableCodesSnapshot = table?.Code,
                OrderNumbersSnapshot = order.Number,
            };
            timestampPatches.Add((bill, soldAt));

            foreach (var line in lines)
            {
                var product = catalog.ProductsById[line.ProductId];
                var billLine = new BillLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BillId = billId,
                    SalesOrderLineId = line.Id,
                    ProductId = line.ProductId,
                    ProductName = product.Name,
                    ProductTypeName = catalog.ProductTypeNames.GetValueOrDefault(line.ProductId, "Otros"),
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    LineTotal = line.LineTotal,
                    ImpoconsumoAmount = tax > 0
                        ? decimal.Round(line.LineTotal / subtotal * tax, 2, MidpointRounding.AwayFromZero)
                        : 0m,
                };
                billLines.Add(billLine);
                timestampPatches.Add((billLine, soldAt));
            }

            billOrderLink = new BillSalesOrder
            {
                TenantId = tenantId,
                BillId = billId,
                SalesOrderId = orderId,
            };

            billSeq++;
        }

        return new PaidSaleBundle(order, lines, invoice, payment, bill, billLines, billOrderLink);
    }
}
