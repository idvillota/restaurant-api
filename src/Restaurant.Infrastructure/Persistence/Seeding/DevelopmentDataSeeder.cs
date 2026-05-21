using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Restaurant.Application.Common;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Authorization;

namespace Restaurant.Infrastructure.Persistence.Seeding;

public static class DevelopmentDataSeeder
{
    private static Guid ChildId(int family, int index) =>
        Guid.Parse($"00000000-0000-400{family}-8000-{index:D12}");

    private const int RecipeGuidFamily = 2;
    private const int PurchaseLineGuidFamily = 3;
    private const int SalesLineGuidFamily = 4;
    private const int ReservationTableGuidFamily = 5;

    public static async Task SeedAsync(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ILogger logger,
        ICurrentTenantContext? tenantContext = null,
        IHostEnvironment? hostEnvironment = null,
        IProductImageStorage? productImageStorage = null,
        CancellationToken cancellationToken = default)
    {
        Guid? previousTenantId = tenantContext?.TenantId;
        if (tenantContext is not null)
            tenantContext.TenantId = DevelopmentSeedIds.TenantId;

        try
        {
            await SeedCoreAsync(db, passwordHasher, logger, hostEnvironment, productImageStorage, cancellationToken);
        }
        finally
        {
            if (tenantContext is not null)
                tenantContext.TenantId = previousTenantId;
        }
    }

    private static async Task SeedCoreAsync(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ILogger logger,
        IHostEnvironment? hostEnvironment,
        IProductImageStorage? productImageStorage,
        CancellationToken cancellationToken)
    {
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == DevelopmentSeedIds.TenantSlug, cancellationToken))
        {
            logger.LogInformation(
                "Development seed skipped: tenant '{Slug}' already exists.",
                DevelopmentSeedIds.TenantSlug);
            return;
        }

        var tenantId = DevelopmentSeedIds.TenantId;
        var utc = DateTime.UtcNow;

        await SeedIdentityAsync(db, passwordHasher, tenantId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await SeedCatalogAsync(db, tenantId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (hostEnvironment is not null && productImageStorage is not null)
        {
            await DevelopmentSeedAssets.ApplyProductImagesAsync(
                db,
                tenantId,
                hostEnvironment,
                productImageStorage,
                logger,
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await SeedProcurementAsync(db, tenantId, utc, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await SeedOperationsAsync(db, tenantId, utc, cancellationToken);
        await SeedSalesAsync(db, tenantId, utc, cancellationToken);
        await SeedStaffAsync(db, tenantId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Development seed completed for '{Slug}'. Logins: {AdminEmail} or {DemoEmail} (password: {Password})",
            DevelopmentSeedIds.TenantSlug,
            DevelopmentSeedIds.AdminEmail,
            DevelopmentSeedIds.DemoTestEmail,
            DevelopmentSeedIds.AdminPassword);
    }

    private static async Task SeedIdentityAsync(
        ApplicationDbContext db,
        IPasswordHasher passwordHasher,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await db.Tenants.AddAsync(
            new Tenant
            {
                Id = tenantId,
                Name = "Bistró Demo",
                Slug = DevelopmentSeedIds.TenantSlug,
                TimeZoneId = "America/Bogota",
                CurrencyCode = "COP",
                IsActive = true,
            },
            cancellationToken);

        await db.Users.AddRangeAsync(
            [
                new User
                {
                    Id = DevelopmentSeedIds.AdminUserId,
                    Email = DevelopmentSeedIds.AdminEmail,
                    NormalizedEmail = DevelopmentSeedIds.AdminEmail.ToUpperInvariant(),
                    PasswordHash = passwordHasher.Hash(DevelopmentSeedIds.AdminPassword),
                    DisplayName = "Dueño demo",
                },
                new User
                {
                    Id = DevelopmentSeedIds.DemoTestUserId,
                    Email = DevelopmentSeedIds.DemoTestEmail,
                    NormalizedEmail = DevelopmentSeedIds.DemoTestEmail.ToUpperInvariant(),
                    PasswordHash = passwordHasher.Hash(DevelopmentSeedIds.AdminPassword),
                    DisplayName = "Iván Villota",
                },
            ],
            cancellationToken);

        var roles = new[]
        {
            new Role { Id = DevelopmentSeedIds.OwnerRoleId, TenantId = tenantId, Name = SystemRoles.Owner, NormalizedName = "OWNER" },
            new Role { Id = DevelopmentSeedIds.ManagerRoleId, TenantId = tenantId, Name = SystemRoles.Manager, NormalizedName = "MANAGER" },
            new Role { Id = DevelopmentSeedIds.StaffRoleId, TenantId = tenantId, Name = SystemRoles.Staff, NormalizedName = "STAFF" },
        };
        await db.Roles.AddRangeAsync(roles, cancellationToken);

        var tenantUser = new TenantUser
        {
            Id = DevelopmentSeedIds.AdminTenantUserId,
            TenantId = tenantId,
            UserId = DevelopmentSeedIds.AdminUserId,
            IsActive = true,
        };
        tenantUser.TenantUserRoles.Add(
            new TenantUserRole
            {
                Id = Guid.Parse("a1000007-0007-4007-8007-000000000007"),
                TenantId = tenantId,
                TenantUserId = tenantUser.Id,
                RoleId = DevelopmentSeedIds.OwnerRoleId,
            });
        await db.TenantUsers.AddAsync(tenantUser, cancellationToken);

        var demoTestTenantUser = new TenantUser
        {
            Id = DevelopmentSeedIds.DemoTestTenantUserId,
            TenantId = tenantId,
            UserId = DevelopmentSeedIds.DemoTestUserId,
            IsActive = true,
        };
        demoTestTenantUser.TenantUserRoles.Add(
            new TenantUserRole
            {
                Id = Guid.Parse("a100000a-0010-4010-8010-00000000000a"),
                TenantId = tenantId,
                TenantUserId = demoTestTenantUser.Id,
                RoleId = DevelopmentSeedIds.OwnerRoleId,
            });
        await db.TenantUsers.AddAsync(demoTestTenantUser, cancellationToken);
    }

    private static async Task SeedCatalogAsync(ApplicationDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var categoryNames = new[]
        {
            "Verduras", "Lácteos", "Carnes", "Despensa", "Bebidas", "Especias", "Aceites", "Panadería", "Congelados", "Guarniciones",
        };

        for (var i = 0; i < categoryNames.Length; i++)
        {
            await db.IngredientCategories.AddAsync(
                new IngredientCategory
                {
                    Id = DevelopmentSeedIds.IngredientCategoryIds[i],
                    TenantId = tenantId,
                    Name = categoryNames[i],
                    Description = $"Categoría demo: {categoryNames[i]}",
                    SortOrder = i,
                    IsActive = true,
                },
                cancellationToken);
        }

        var ingredients = new (string Name, IngredientUnit Unit, Guid CategoryId, decimal? Reorder)[]
        {
            ("Tomates", IngredientUnit.Gram, DevelopmentSeedIds.IngredientCategoryIds[0], 5000m),
            ("Mozzarella", IngredientUnit.Gram, DevelopmentSeedIds.IngredientCategoryIds[1], 3000m),
            ("Pechuga de pollo", IngredientUnit.Gram, DevelopmentSeedIds.IngredientCategoryIds[2], 4000m),
            ("Pasta penne", IngredientUnit.Gram, DevelopmentSeedIds.IngredientCategoryIds[3], 6000m),
            ("Aceite de oliva", IngredientUnit.Milliliter, DevelopmentSeedIds.IngredientCategoryIds[6], 2000m),
            ("Albahaca", IngredientUnit.Gram, DevelopmentSeedIds.IngredientCategoryIds[9], 200m),
            ("Carne molida", IngredientUnit.Gram, DevelopmentSeedIds.IngredientCategoryIds[2], 5000m),
            ("Pan de hamburguesa", IngredientUnit.Unit, DevelopmentSeedIds.IngredientCategoryIds[7], 24m),
            ("Botella Coca-Cola 500 ml", IngredientUnit.Unit, DevelopmentSeedIds.IngredientCategoryIds[4], 12m),
            ("Botella agua con gas 500 ml", IngredientUnit.Unit, DevelopmentSeedIds.IngredientCategoryIds[4], 12m),
            ("Sal marina", IngredientUnit.Gram, DevelopmentSeedIds.IngredientCategoryIds[5], 1000m),
            ("Harina 00", IngredientUnit.Gram, DevelopmentSeedIds.IngredientCategoryIds[3], 8000m),
        };

        for (var i = 0; i < ingredients.Length; i++)
        {
            var (name, unit, categoryId, reorder) = ingredients[i];
            await db.Ingredients.AddAsync(
                new Ingredient
                {
                    Id = DevelopmentSeedIds.IngredientIds[i],
                    TenantId = tenantId,
                    IngredientCategoryId = categoryId,
                    Name = name,
                    Unit = unit,
                    UnitCost = null,
                    StockQuantity = null,
                    ReorderLevel = reorder,
                    IsActive = true,
                },
                cancellationToken);
        }

        var typeNames = new[]
        {
            "Pizzas", "Pastas", "Ensaladas", "Hamburguesas", "Postres", "Refrescos", "Cerveza", "Vino", "Entradas", "Especiales",
        };

        for (var i = 0; i < typeNames.Length; i++)
        {
            await db.ProductTypes.AddAsync(
                new ProductType
                {
                    Id = DevelopmentSeedIds.ProductTypeIds[i],
                    TenantId = tenantId,
                    Name = typeNames[i],
                    Description = $"Sección del menú: {typeNames[i]}",
                    SortOrder = i,
                    IsActive = true,
                },
                cancellationToken);
        }

        var products = new (string Name, Guid TypeId, EProductType Kind, decimal Price, string? Sku)[]
        {
            ("Pizza margarita", DevelopmentSeedIds.ProductTypeIds[0], EProductType.Prepared, 32000m, "PIZ-001"),
            ("Penne arrabbiata", DevelopmentSeedIds.ProductTypeIds[1], EProductType.Prepared, 28000m, "PAS-001"),
            ("Ensalada César", DevelopmentSeedIds.ProductTypeIds[2], EProductType.Prepared, 24000m, "SAL-001"),
            ("Hamburguesa clásica", DevelopmentSeedIds.ProductTypeIds[3], EProductType.Prepared, 35000m, "BRG-001"),
            ("Tiramisú", DevelopmentSeedIds.ProductTypeIds[4], EProductType.Prepared, 15000m, "DES-001"),
            ("Cola", DevelopmentSeedIds.ProductTypeIds[5], EProductType.Resale, 6000m, "DRK-001"),
            ("Agua con gas", DevelopmentSeedIds.ProductTypeIds[5], EProductType.Resale, 5000m, "DRK-002"),
            ("Pan de ajo", DevelopmentSeedIds.ProductTypeIds[8], EProductType.Prepared, 12000m, "APP-001"),
            ("Sopa del día", DevelopmentSeedIds.ProductTypeIds[9], EProductType.Prepared, 14000m, "SPC-001"),
            ("Pizza pepperoni", DevelopmentSeedIds.ProductTypeIds[0], EProductType.Prepared, 36000m, "PIZ-002"),
            ("Pollo a la parrilla", DevelopmentSeedIds.ProductTypeIds[9], EProductType.Prepared, 42000m, "SPC-002"),
            ("Limonada de la casa", DevelopmentSeedIds.ProductTypeIds[5], EProductType.Prepared, 8000m, "DRK-003"),
        };

        for (var i = 0; i < products.Length; i++)
        {
            var (name, typeId, kind, price, sku) = products[i];
            await db.Products.AddAsync(
                new Product
                {
                    Id = DevelopmentSeedIds.ProductIds[i],
                    TenantId = tenantId,
                    ProductTypeId = typeId,
                    CompositionType = kind,
                    Name = name,
                    Description = $"Plato del menú demo — {name}",
                    Sku = sku,
                    UnitPrice = price,
                    IsActive = true,
                },
                cancellationToken);
        }

        var recipes = new (Guid ProductId, Guid IngredientId, decimal Qty)[]
        {
            (DevelopmentSeedIds.ProductIds[0], DevelopmentSeedIds.IngredientIds[0], 250m),
            (DevelopmentSeedIds.ProductIds[0], DevelopmentSeedIds.IngredientIds[1], 200m),
            (DevelopmentSeedIds.ProductIds[0], DevelopmentSeedIds.IngredientIds[11], 150m),
            (DevelopmentSeedIds.ProductIds[0], DevelopmentSeedIds.IngredientIds[4], 20m),
            (DevelopmentSeedIds.ProductIds[1], DevelopmentSeedIds.IngredientIds[3], 180m),
            (DevelopmentSeedIds.ProductIds[1], DevelopmentSeedIds.IngredientIds[4], 30m),
            (DevelopmentSeedIds.ProductIds[3], DevelopmentSeedIds.IngredientIds[6], 180m),
            (DevelopmentSeedIds.ProductIds[3], DevelopmentSeedIds.IngredientIds[7], 1m),
            (DevelopmentSeedIds.ProductIds[5], DevelopmentSeedIds.IngredientIds[8], 1m),
            (DevelopmentSeedIds.ProductIds[6], DevelopmentSeedIds.IngredientIds[9], 1m),
            (DevelopmentSeedIds.ProductIds[9], DevelopmentSeedIds.IngredientIds[0], 200m),
            (DevelopmentSeedIds.ProductIds[9], DevelopmentSeedIds.IngredientIds[1], 150m),
            (DevelopmentSeedIds.ProductIds[2], DevelopmentSeedIds.IngredientIds[0], 150m),
            (DevelopmentSeedIds.ProductIds[2], DevelopmentSeedIds.IngredientIds[4], 10m),
            (DevelopmentSeedIds.ProductIds[4], DevelopmentSeedIds.IngredientIds[1], 100m),
            (DevelopmentSeedIds.ProductIds[8], DevelopmentSeedIds.IngredientIds[11], 80m),
            (DevelopmentSeedIds.ProductIds[8], DevelopmentSeedIds.IngredientIds[4], 20m),
            (DevelopmentSeedIds.ProductIds[10], DevelopmentSeedIds.IngredientIds[2], 220m),
            (DevelopmentSeedIds.ProductIds[10], DevelopmentSeedIds.IngredientIds[4], 10m),
            (DevelopmentSeedIds.ProductIds[11], DevelopmentSeedIds.IngredientIds[9], 1m),
            (DevelopmentSeedIds.ProductIds[11], DevelopmentSeedIds.IngredientIds[0], 50m),
        };

        var recipeIndex = 0;
        foreach (var (productId, ingredientId, qty) in recipes)
        {
            await db.ProductIngredients.AddAsync(
                new ProductIngredient
                {
                    Id = ChildId(RecipeGuidFamily, recipeIndex + 1),
                    TenantId = tenantId,
                    ProductId = productId,
                    IngredientId = ingredientId,
                    Quantity = qty,
                },
                cancellationToken);
            recipeIndex++;
        }
    }

    private static async Task SeedProcurementAsync(
        ApplicationDbContext db,
        Guid tenantId,
        DateTime utc,
        CancellationToken cancellationToken)
    {
        var providers = new (string Name, string Contact, string Address, string? Phone)[]
        {
            ("Frescos del Campo S.A.", "María López", "Calle Mercado 120", "555-0101"),
            ("Lácteos Directos", "Jaime Chen", "Av. Lácteos 88", "555-0102"),
            ("Carnes Prime", "Alex Rivera", "Blvd. Industrial 45", "555-0103"),
            ("Importaciones Mediterráneas", "Sofía Neri", "Calle Puerto 9", "555-0104"),
            ("Depósito de Bebidas", "Tomás Walsh", "Carrera Bodega 200", "555-0105"),
            ("Suministros de Panadería", "Elena Rossi", "Calle Horno 3", "555-0106"),
            ("Ruta de Especias", "Omar Hassan", "Av. Bazar 77", "555-0107"),
            ("Alimentos Congelados S.A.", "Pat Kim", "Km 5 Vía Frío", "555-0108"),
            ("Verduras Hoja Verde", "Ana Silva", "Carrera Jardín 14", "555-0109"),
            ("Depósito Restaurantes", "Chris Park", "Av. Mayorista 900", "555-0110"),
        };

        for (var i = 0; i < providers.Length; i++)
        {
            var (name, contact, address, phone) = providers[i];
            await db.Providers.AddAsync(
                new Provider
                {
                    Id = DevelopmentSeedIds.ProviderIds[i],
                    TenantId = tenantId,
                    Name = name,
                    ContactName = contact,
                    Address = address,
                    Phone = phone,
                    Email = $"orders+{i + 1}@demo-supplier.example",
                    IsActive = true,
                },
                cancellationToken);
        }

        // Cantidades y precios unitarios enteros (g, ml o und; COP por unidad de medida).
        var purchaseSpecs = new (int ProviderIndex, string Bill, int DaysAgo, (int Ingredient, decimal Qty, decimal Price)[] Lines)[]
        {
            (0, "FF-2026-001", 30, [(0, 20000m, 6m), (5, 500m, 80m)]),
            (1, "DD-2026-014", 28, [(1, 10000m, 28m)]),
            (2, "PM-2026-088", 25, [(2, 15000m, 32m), (6, 12000m, 38m)]),
            (3, "MI-2026-033", 22, [(3, 25000m, 4m), (4, 8000m, 28m)]),
            (4, "BD-2026-201", 20, [(8, 48m, 3200m), (9, 36m, 2800m)]),
            (0, "FF-2026-045", 18, [(0, 15000m, 6m)]),
            (7, "FR-2026-012", 15, [(11, 40000m, 4m)]),
            (5, "BS-2026-007", 12, [(7, 60m, 1200m)]),
            (8, "GL-2026-019", 10, [(0, 18000m, 6m), (5, 300m, 85m)]),
            (9, "RD-2026-500", 5, [(1, 8000m, 29m), (2, 10000m, 33m), (4, 5000m, 30m)]),
        };

        var ingredientEntities = await db.Ingredients
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId)
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        if (ingredientEntities.Count != DevelopmentSeedIds.IngredientIds.Length)
        {
            throw new InvalidOperationException(
                $"Seed expected {DevelopmentSeedIds.IngredientIds.Length} ingredients in the database, found {ingredientEntities.Count}.");
        }

        for (var p = 0; p < purchaseSpecs.Length; p++)
        {
            var (providerIndex, bill, daysAgo, lineSpecs) = purchaseSpecs[p];
            var purchaseId = DevelopmentSeedIds.PurchaseIds[p];
            decimal subtotal = 0;
            var lineEntities = new List<PurchaseLine>();

            for (var l = 0; l < lineSpecs.Length; l++)
            {
                var (ingIndex, qty, price) = lineSpecs[l];
                var ingredientId = DevelopmentSeedIds.IngredientIds[ingIndex];
                var lineTotal = qty * price;
                subtotal += lineTotal;

                lineEntities.Add(
                    new PurchaseLine
                    {
                        Id = ChildId(PurchaseLineGuidFamily, p * 10 + l + 1),
                        TenantId = tenantId,
                        PurchaseId = purchaseId,
                        IngredientId = ingredientId,
                        Quantity = qty,
                        UnitPrice = price,
                        LineTotal = lineTotal,
                    });
            }

            subtotal = decimal.Round(subtotal, 0, MidpointRounding.AwayFromZero);
            var tax = decimal.Round(subtotal * 0.19m, 0, MidpointRounding.AwayFromZero);

            var purchasedAt = utc.AddDays(-daysAgo);
            // Mitad al contado (misma fecha); mitad a crédito (pago un mes después).
            var paymentAt = p < purchaseSpecs.Length / 2 ? purchasedAt : purchasedAt.AddMonths(1);

            await db.Purchases.AddAsync(
                new Purchase
                {
                    Id = purchaseId,
                    TenantId = tenantId,
                    ProviderId = DevelopmentSeedIds.ProviderIds[providerIndex],
                    BillNumber = bill,
                    PurchasedAtUtc = purchasedAt,
                    PaymentDateUtc = paymentAt,
                    Subtotal = subtotal,
                    TaxAmount = tax,
                    Total = subtotal + tax,
                    Notes = "Compra de demostración",
                },
                cancellationToken);

            await db.PurchaseLines.AddRangeAsync(lineEntities, cancellationToken);

            foreach (var line in lineEntities)
            {
                if (!ingredientEntities.TryGetValue(line.IngredientId, out var ingredient))
                {
                    throw new InvalidOperationException(
                        $"Seed purchase line references unknown ingredient '{line.IngredientId}'.");
                }

                var previousQuantity = ingredient.StockQuantity;
                var previousUnitCost = ingredient.UnitCost;
                ingredient.UnitCost = InventoryCosting.ComputeWeightedAverageUnitCost(
                    previousQuantity,
                    previousUnitCost,
                    line.Quantity,
                    line.UnitPrice);
                ingredient.StockQuantity = InventoryCosting.AddStock(previousQuantity, line.Quantity);
            }
        }
    }

    private static async Task SeedOperationsAsync(
        ApplicationDbContext db,
        Guid tenantId,
        DateTime utc,
        CancellationToken cancellationToken)
    {
        var zones = new[] { "Salón", "Salón", "Salón", "Terraza", "Terraza", "Barra", "Barra", "Privado", "Privado", "Ventana", "Ventana", "Rincón" };
        var capacities = new[] { 2, 2, 4, 4, 6, 2, 4, 8, 10, 2, 4, 6 };

        var layoutIndexByZone = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < DevelopmentSeedIds.DiningTableIds.Length; i++)
        {
            var status = i switch
            {
                >= 4 and <= 6 => ETableStatus.Reserved,
                7 => ETableStatus.Busy,
                _ => ETableStatus.Available,
            };

            var zone = zones[i];
            var zoneIndex = layoutIndexByZone.GetValueOrDefault(zone);
            layoutIndexByZone[zone] = zoneIndex + 1;
            var col = zoneIndex % 4;
            var row = zoneIndex / 4;

            await db.DiningTables.AddAsync(
                new DiningTable
                {
                    Id = DevelopmentSeedIds.DiningTableIds[i],
                    TenantId = tenantId,
                    Code = $"T-{i + 1:00}",
                    Capacity = capacities[i],
                    Zone = zone,
                    LayoutX = 8 + col * 22,
                    LayoutY = 10 + row * 24,
                    Status = status,
                    IsActive = true,
                },
                cancellationToken);
        }

        var customers = new (string Name, string? Email, string? Phone)[]
        {
            ("Cliente sin cita", null, null),
            ("Ana Martínez", "ana.martinez@example.com", "555-1001"),
            ("Juan Pérez", "juan.perez@example.com", "555-1002"),
            ("Li Wei", "li.wei@example.com", "555-1003"),
            ("Emma Rodríguez", "emma.rodriguez@example.com", "555-1004"),
            ("Carlos Díaz", "carlos.diaz@example.com", "555-1005"),
            ("Priya Patel", "priya.patel@example.com", "555-1006"),
            ("Noah Wilson", "noah.wilson@example.com", "555-1007"),
            ("Sara Kim", "sara.kim@example.com", "555-1008"),
            ("Catering oficina", "catering@acme.example", "555-1009"),
        };

        for (var i = 0; i < customers.Length; i++)
        {
            var (name, email, phone) = customers[i];
            await db.Customers.AddAsync(
                new Customer
                {
                    Id = DevelopmentSeedIds.CustomerIds[i],
                    TenantId = tenantId,
                    Name = name,
                    Email = email,
                    Phone = phone,
                    IsActive = true,
                },
                cancellationToken);
        }

        var reservationSpecs = new (string Contact, int Party, int DaysFromNow, int DurationMin, ReservationStatus Status, int? TableIndex, Guid? CustomerId)[]
        {
            ("Ana Martínez", 2, 1, 120, ReservationStatus.Confirmed, 4, DevelopmentSeedIds.CustomerIds[1]),
            ("Juan Pérez", 4, 1, 90, ReservationStatus.Confirmed, 5, DevelopmentSeedIds.CustomerIds[2]),
            ("Li Wei", 6, 2, 150, ReservationStatus.Pending, 6, DevelopmentSeedIds.CustomerIds[3]),
            ("Emma Rodríguez", 2, 2, 120, ReservationStatus.Confirmed, null, DevelopmentSeedIds.CustomerIds[4]),
            ("Carlos Díaz", 8, 3, 180, ReservationStatus.Confirmed, 8, DevelopmentSeedIds.CustomerIds[5]),
            ("Priya Patel", 3, 0, 90, ReservationStatus.Seated, 7, DevelopmentSeedIds.CustomerIds[6]),
            ("Noah Wilson", 2, -1, 120, ReservationStatus.Completed, null, DevelopmentSeedIds.CustomerIds[7]),
            ("Sara Kim", 4, 4, 120, ReservationStatus.Pending, 9, DevelopmentSeedIds.CustomerIds[8]),
            ("Catering oficina", 12, 7, 240, ReservationStatus.Confirmed, 8, DevelopmentSeedIds.CustomerIds[9]),
            ("Cliente sin cita", 2, 0, 60, ReservationStatus.Cancelled, null, DevelopmentSeedIds.CustomerIds[0]),
        };

        for (var r = 0; r < reservationSpecs.Length; r++)
        {
            var (contact, party, days, duration, status, tableIndex, customerId) = reservationSpecs[r];
            var start = utc.AddDays(days).AddHours(12 + r);
            var reservationId = DevelopmentSeedIds.ReservationIds[r];

            await db.Reservations.AddAsync(
                new Reservation
                {
                    Id = reservationId,
                    TenantId = tenantId,
                    CustomerId = customerId,
                    ContactName = contact,
                    PartySize = party,
                    StartAtUtc = start,
                    EndAtUtc = start.AddMinutes(duration),
                    Status = status,
                    Notes = "Reserva de demostración",
                },
                cancellationToken);

            if (tableIndex.HasValue)
            {
                await db.ReservationTables.AddAsync(
                    new ReservationTable
                    {
                        Id = ChildId(ReservationTableGuidFamily, r + 1),
                        TenantId = tenantId,
                        ReservationId = reservationId,
                        DiningTableId = DevelopmentSeedIds.DiningTableIds[tableIndex.Value],
                    },
                    cancellationToken);
            }
        }
    }

    private static async Task SeedSalesAsync(
        ApplicationDbContext db,
        Guid tenantId,
        DateTime utc,
        CancellationToken cancellationToken)
    {
        var orderSpecs = new (string Number, SalesOrderStatus Status, Guid? CustomerId, int DaysAgo, (int Product, decimal Qty)[] Lines)[]
        {
            ("SO-1001", SalesOrderStatus.Paid, DevelopmentSeedIds.CustomerIds[1], 2, [(0, 1m), (5, 2m)]),
            ("SO-1002", SalesOrderStatus.Paid, DevelopmentSeedIds.CustomerIds[2], 2, [(3, 2m), (7, 1m)]),
            ("SO-1003", SalesOrderStatus.Open, DevelopmentSeedIds.CustomerIds[3], 0, [(1, 1m), (2, 1m)]),
            ("SO-1004", SalesOrderStatus.Paid, null, 1, [(0, 2m), (9, 1m)]),
            ("SO-1005", SalesOrderStatus.Paid, DevelopmentSeedIds.CustomerIds[5], 3, [(10, 1m), (6, 2m)]),
            ("SO-1006", SalesOrderStatus.Voided, DevelopmentSeedIds.CustomerIds[6], 4, [(4, 1m)]),
            ("SO-1007", SalesOrderStatus.Paid, DevelopmentSeedIds.CustomerIds[7], 5, [(3, 1m), (5, 1m), (8, 1m)]),
            ("SO-1008", SalesOrderStatus.Draft, DevelopmentSeedIds.CustomerIds[8], 0, [(2, 1m)]),
            ("SO-1009", SalesOrderStatus.Paid, DevelopmentSeedIds.CustomerIds[9], 1, [(0, 3m), (1, 2m), (5, 5m)]),
            ("SO-1010", SalesOrderStatus.Paid, DevelopmentSeedIds.CustomerIds[4], 6, [(11, 2m), (7, 2m)]),
        };

        var productPrices = await db.Products
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .ToDictionaryAsync(p => p.Id, p => p.UnitPrice, cancellationToken);

        if (productPrices.Count != DevelopmentSeedIds.ProductIds.Length)
        {
            throw new InvalidOperationException(
                $"Seed expected {DevelopmentSeedIds.ProductIds.Length} products in the database, found {productPrices.Count}.");
        }

        for (var o = 0; o < orderSpecs.Length; o++)
        {
            var (number, status, customerId, daysAgo, lineSpecs) = orderSpecs[o];
            var orderId = DevelopmentSeedIds.SalesOrderIds[o];
            decimal subtotal = 0;
            var lineEntities = new List<SalesOrderLine>();

            for (var l = 0; l < lineSpecs.Length; l++)
            {
                var (productIndex, qty) = lineSpecs[l];
                var productId = DevelopmentSeedIds.ProductIds[productIndex];
                if (!productPrices.TryGetValue(productId, out var unitPrice))
                {
                    throw new InvalidOperationException(
                        $"Seed sales line references unknown product '{productId}'.");
                }
                var lineTotal = qty * unitPrice;
                subtotal += lineTotal;

                lineEntities.Add(
                    new SalesOrderLine
                    {
                        Id = ChildId(SalesLineGuidFamily, o * 10 + l + 1),
                        TenantId = tenantId,
                        SalesOrderId = orderId,
                        ProductId = productId,
                        Quantity = qty,
                        UnitPrice = unitPrice,
                        LineTotal = lineTotal,
                    });
            }

            subtotal = decimal.Round(subtotal, 0, MidpointRounding.AwayFromZero);
            var tax = decimal.Round(subtotal * 0.19m, 0, MidpointRounding.AwayFromZero);
            var total = subtotal + tax;
            var closedAt = status is SalesOrderStatus.Paid or SalesOrderStatus.Voided
                ? utc.AddDays(-daysAgo)
                : (DateTime?)null;

            await db.SalesOrders.AddAsync(
                new SalesOrder
                {
                    Id = orderId,
                    TenantId = tenantId,
                    CustomerId = customerId,
                    Number = number,
                    Status = status,
                    Subtotal = subtotal,
                    TaxAmount = tax,
                    Total = total,
                    ClosedAtUtc = closedAt,
                },
                cancellationToken);

            await db.SalesOrderLines.AddRangeAsync(lineEntities, cancellationToken);

            var invoiceStatus = status switch
            {
                SalesOrderStatus.Paid => InvoiceStatus.Paid,
                SalesOrderStatus.Voided => InvoiceStatus.Voided,
                SalesOrderStatus.Open => InvoiceStatus.Issued,
                _ => InvoiceStatus.Draft,
            };

            await db.Invoices.AddAsync(
                new Invoice
                {
                    Id = DevelopmentSeedIds.InvoiceIds[o],
                    TenantId = tenantId,
                    SalesOrderId = orderId,
                    CustomerId = customerId,
                    Number = $"INV-{number}",
                    Status = invoiceStatus,
                    Subtotal = subtotal,
                    TaxAmount = tax,
                    Total = total,
                    IssuedAtUtc = utc.AddDays(-daysAgo),
                    DueAtUtc = utc.AddDays(-daysAgo + 14),
                },
                cancellationToken);

            if (status == SalesOrderStatus.Paid)
            {
                await db.Payments.AddAsync(
                    new Payment
                    {
                        Id = DevelopmentSeedIds.PaymentIds[o],
                        TenantId = tenantId,
                        SalesOrderId = orderId,
                        InvoiceId = DevelopmentSeedIds.InvoiceIds[o],
                        Amount = total,
                        Method = (o % 3) switch
                        {
                            0 => PaymentMethod.Cash,
                            1 => PaymentMethod.Card,
                            _ => PaymentMethod.Transfer,
                        },
                        Status = PaymentStatus.Completed,
                        ExternalReference = $"PAY-{number}",
                        PaidAtUtc = utc.AddDays(-daysAgo),
                    },
                    cancellationToken);
            }
        }
    }

    private static async Task SeedStaffAsync(ApplicationDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var staff = new (string Name, string Title, DateOnly Hired)[]
        {
            ("Dueño demo", "Propietario", new DateOnly(2020, 1, 15)),
            ("Mía Turner", "Gerente general", new DateOnly(2021, 3, 1)),
            ("Diego Álvarez", "Chef ejecutivo", new DateOnly(2019, 6, 10)),
            ("Julie Park", "Sous chef", new DateOnly(2022, 2, 20)),
            ("Sam Ortiz", "Cocinero de línea", new DateOnly(2023, 8, 5)),
            ("Riley Chen", "Mesero/a", new DateOnly(2024, 1, 10)),
            ("Jordan Lee", "Mesero/a", new DateOnly(2024, 4, 18)),
            ("Taylor Brooks", "Anfitrión", new DateOnly(2023, 11, 2)),
            ("Casey Morgan", "Bartender", new DateOnly(2022, 7, 25)),
            ("Quinn Adams", "Lavaplatos", new DateOnly(2025, 1, 8)),
        };

        for (var i = 0; i < staff.Length; i++)
        {
            var (name, title, hired) = staff[i];
            await db.Employees.AddAsync(
                new Employee
                {
                    Id = DevelopmentSeedIds.EmployeeIds[i],
                    TenantId = tenantId,
                    TenantUserId = i == 0 ? DevelopmentSeedIds.AdminTenantUserId : null,
                    FullName = name,
                    JobTitle = title,
                    HiredOn = hired,
                    IsActive = true,
                },
                cancellationToken);
        }
    }
}
