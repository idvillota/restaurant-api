using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Restaurant.Application.Authorization;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
using Restaurant.Application.Features.Organization.RolePermissions;
using Restaurant.Application.Features.Platform;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Authorization;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Infrastructure.Persistence.Seeding;
using Restaurant.Infrastructure.Services.InitialData;

namespace Restaurant.Infrastructure.Services;

public sealed class TenantInitialDataImportService : ITenantInitialDataImportService
{
    private static readonly (string Email, string DisplayName, string Role)[] StarterUsers =
    [
        ("idvillota@gmail.com", "Administrador", SystemRoles.Administrator),
        ("gerente@gmail.com", "Gerente", SystemRoles.Manager),
        ("mesera@gmail.com", "Mesera", SystemRoles.Waitress),
        ("cajero@gmail.com", "Cajero", SystemRoles.Cashier),
    ];

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRolePermissionService _rolePermissions;
    private readonly IKitchenPrinterService _kitchenPrinters;
    private readonly PlatformOptions _platform;

    public TenantInitialDataImportService(
        ApplicationDbContext db,
        ICurrentTenantContext tenantContext,
        IPasswordHasher passwordHasher,
        IRolePermissionService rolePermissions,
        IKitchenPrinterService kitchenPrinters,
        IOptions<PlatformOptions> platform)
    {
        _db = db;
        _tenantContext = tenantContext;
        _passwordHasher = passwordHasher;
        _rolePermissions = rolePermissions;
        _kitchenPrinters = kitchenPrinters;
        _platform = platform.Value;
    }

    public byte[] BuildTemplate() => InitialDataExcelParser.BuildTemplate();

    public async Task<TenantInitialDataImportResultDto> ImportAsync(
        Stream excelStream,
        CancellationToken cancellationToken = default)
    {
        var workbook = InitialDataExcelParser.Parse(excelStream, out var parseErrors);
        if (parseErrors.Count > 0)
            throw new TenantInitialDataValidationException(parseErrors);

        var slug = workbook.Tenant?.Slug.Trim().ToLowerInvariant() ?? string.Empty;
        var slugExists = !string.IsNullOrEmpty(slug)
            && await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == slug, cancellationToken);

        var validationErrors = InitialDataValidator.Validate(workbook, slugExists);
        if (validationErrors.Count > 0)
            throw new TenantInitialDataValidationException(validationErrors);

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await PersistAsync(workbook, cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<TenantInitialDataImportResultDto> PersistAsync(
        InitialDataWorkbook workbook,
        CancellationToken cancellationToken)
    {
        var tenantRow = workbook.Tenant!;
        var billing = workbook.Billing!;
        var tenantId = Guid.NewGuid();

        _tenantContext.TenantId = tenantId;

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = tenantRow.Name.Trim(),
            Slug = tenantRow.Slug.Trim().ToLowerInvariant(),
            TimeZoneId = string.IsNullOrWhiteSpace(tenantRow.TimeZoneId) ? "America/Bogota" : tenantRow.TimeZoneId.Trim(),
            CurrencyCode = tenantRow.CurrencyCode.Trim().ToUpperInvariant(),
            IsActive = true,
        };

        var settings = new TenantSettings
        {
            TenantId = tenantId,
            TradeName = billing.TradeName.Trim(),
            LegalName = billing.LegalName.Trim(),
            TaxId = billing.TaxId.Trim(),
            TaxRegime = billing.TaxRegime.Trim(),
            LegalRepresentative = billing.LegalRepresentative?.Trim(),
            AddressLine = billing.AddressLine.Trim(),
            City = billing.City.Trim(),
            Country = billing.Country.Trim(),
            PostalCode = billing.PostalCode?.Trim(),
            Phone = billing.Phone?.Trim(),
            MaxDiscountPercent = billing.MaxDiscountPercent,
            OperationalDayCutoffHour = billing.OperationalDayCutoffHour,
            ImpoconsumoPercent = billing.ImpoconsumoPercent,
        };

        _db.Tenants.Add(tenant);
        _db.TenantSettings.Add(settings);

        var roles = new[]
        {
            NewRole(tenantId, SystemRoles.Administrator),
            NewRole(tenantId, SystemRoles.Manager),
            NewRole(tenantId, SystemRoles.Waitress),
            NewRole(tenantId, SystemRoles.Cashier),
        };
        _db.Roles.AddRange(roles);

        var password = string.IsNullOrWhiteSpace(_platform.StarterUserPassword)
            ? "Demo123!"
            : _platform.StarterUserPassword;
        var passwordHash = _passwordHasher.Hash(password);
        var userEmails = new List<string>();

        foreach (var (email, displayName, roleName) in StarterUsers)
        {
            var normalized = email.Trim().ToUpperInvariant();
            var user = await _db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);

            if (user is null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email.Trim(),
                    NormalizedEmail = normalized,
                    PasswordHash = passwordHash,
                    DisplayName = displayName,
                };
                _db.Users.Add(user);
            }

            var tenantUser = new TenantUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                IsActive = true,
            };
            var role = roles.First(r => r.Name == roleName);
            tenantUser.TenantUserRoles.Add(new TenantUserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TenantUserId = tenantUser.Id,
                RoleId = role.Id,
            });
            _db.TenantUsers.Add(tenantUser);
            userEmails.Add(user.Email);
        }

        var categoryByName = new Dictionary<string, IngredientCategory>(StringComparer.OrdinalIgnoreCase);
        var sort = 10;
        foreach (var categoryName in workbook.Ingredients
                     .Select(i => i.Category.Trim())
                     .Where(n => n.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var category = new IngredientCategory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = categoryName,
                SortOrder = sort,
                IsActive = true,
            };
            sort += 10;
            categoryByName[categoryName] = category;
            _db.IngredientCategories.Add(category);
        }

        var ingredientByCode = new Dictionary<string, Ingredient>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in workbook.Ingredients)
        {
            var ingredient = new Ingredient
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IngredientCategoryId = categoryByName[row.Category.Trim()].Id,
                Name = row.Name.Trim(),
                Unit = Enum.Parse<IngredientUnit>(row.Unit.Trim(), ignoreCase: true),
                UnitCost = row.UnitCost,
                StockQuantity = row.StockQuantity,
                ReorderLevel = row.ReorderLevel,
                IsActive = row.IsActive,
            };
            ingredientByCode[row.Code.Trim()] = ingredient;
            _db.Ingredients.Add(ingredient);
        }

        var productTypeByCode = new Dictionary<string, ProductType>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in workbook.ProductTypes)
        {
            var productType = new ProductType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = row.Name.Trim(),
                Description = row.Description?.Trim(),
                SortOrder = row.SortOrder,
                IsActive = true,
            };
            productTypeByCode[row.Code.Trim()] = productType;
            _db.ProductTypes.Add(productType);
        }

        var productByCode = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in workbook.Products)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductTypeId = productTypeByCode[row.ProductTypeCode.Trim()].Id,
                CompositionType = Enum.Parse<EProductType>(row.CompositionType.Trim(), ignoreCase: true),
                Name = row.Name.Trim(),
                Description = row.Description?.Trim(),
                Sku = row.Code.Trim(),
                UnitPrice = row.UnitPrice,
                IsActive = row.IsActive,
            };
            productByCode[row.Code.Trim()] = product;
            _db.Products.Add(product);
        }

        foreach (var row in workbook.Recipes)
        {
            _db.ProductIngredients.Add(new ProductIngredient
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = productByCode[row.ProductCode.Trim()].Id,
                IngredientId = ingredientByCode[row.IngredientCode.Trim()].Id,
                Quantity = row.Quantity,
            });
        }

        foreach (var row in workbook.DiningTables)
        {
            _db.DiningTables.Add(new DiningTable
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = row.Code.Trim(),
                Capacity = row.Capacity,
                Zone = row.Zone?.Trim(),
                LayoutX = row.LayoutX,
                LayoutY = row.LayoutY,
                Status = ETableStatus.Available,
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        await IngredientMovementTypeBootstrap.EnsureForTenantAsync(_db, tenantId, cancellationToken);
        await _kitchenPrinters.EnsureDefaultStationAsync(tenantId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var adminRole = roles.First(r => r.Name == SystemRoles.Administrator);
        await _rolePermissions.UpdateRolePermissionsAsync(
            adminRole.Id,
            new UpdateRolePermissionsDto { FeatureCodes = FeatureCodes.All },
            cancellationToken);

        foreach (var role in roles.Where(r => r.Id != adminRole.Id))
        {
            if (!FeatureCatalog.DefaultFeaturesByRole.TryGetValue(role.Name, out var codes))
                continue;

            await _rolePermissions.UpdateRolePermissionsAsync(
                role.Id,
                new UpdateRolePermissionsDto { FeatureCodes = codes },
                cancellationToken);
        }

        return new TenantInitialDataImportResultDto
        {
            TenantId = tenantId,
            Slug = tenant.Slug,
            Name = tenant.Name,
            UsersCreated = userEmails,
            Counts = new TenantInitialDataCountsDto
            {
                ProductTypes = workbook.ProductTypes.Count,
                Products = workbook.Products.Count,
                IngredientCategories = categoryByName.Count,
                Ingredients = workbook.Ingredients.Count,
                Recipes = workbook.Recipes.Count,
                DiningTables = workbook.DiningTables.Count,
            },
        };
    }

    private static Role NewRole(Guid tenantId, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            NormalizedName = name.Trim().ToUpperInvariant(),
        };
}
