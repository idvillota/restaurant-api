using System.Text.RegularExpressions;
using Restaurant.Application.Features.Platform;
using Restaurant.Domain.Enums;

namespace Restaurant.Infrastructure.Services.InitialData;

internal static class InitialDataValidator
{
    private static readonly HashSet<string> AllowedCompositionTypes =
        new(StringComparer.OrdinalIgnoreCase) { "Prepared", "Resale", "Bundle" };

    private static readonly HashSet<string> AllowedUnits =
        new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(IngredientUnit.Unit),
            nameof(IngredientUnit.Kilogram),
            nameof(IngredientUnit.Gram),
            nameof(IngredientUnit.Liter),
            nameof(IngredientUnit.Milliliter),
        };

    public static List<TenantInitialDataErrorDto> Validate(
        InitialDataWorkbook workbook,
        bool slugAlreadyExists)
    {
        var errors = new List<TenantInitialDataErrorDto>();

        ValidateTenant(workbook.Tenant, slugAlreadyExists, errors);
        ValidateBilling(workbook.Billing, errors);
        ValidateProductTypes(workbook.ProductTypes, errors);
        ValidateProducts(workbook.Products, workbook.ProductTypes, errors);
        ValidateIngredients(workbook.Ingredients, errors);
        ValidateRecipes(workbook.Recipes, workbook.Products, workbook.Ingredients, errors);
        ValidateDiningTables(workbook.DiningTables, errors);

        return errors;
    }

    private static void ValidateTenant(TenantRow? tenant, bool slugAlreadyExists, List<TenantInitialDataErrorDto> errors)
    {
        if (tenant is null)
        {
            errors.Add(Err(InitialDataSheetNames.Tenant, null, null, "Datos de tenant requeridos."));
            return;
        }

        if (string.IsNullOrWhiteSpace(tenant.Name))
            errors.Add(Err(InitialDataSheetNames.Tenant, tenant.RowNumber, "Name", "Name es obligatorio."));

        if (string.IsNullOrWhiteSpace(tenant.Slug))
            errors.Add(Err(InitialDataSheetNames.Tenant, tenant.RowNumber, "Slug", "Slug es obligatorio."));
        else if (!Regex.IsMatch(tenant.Slug, @"^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            errors.Add(Err(InitialDataSheetNames.Tenant, tenant.RowNumber, "Slug", "Slug debe ser minúsculas, números y guiones (ej. restaurante-demo)."));
        else if (slugAlreadyExists)
            errors.Add(Err(InitialDataSheetNames.Tenant, tenant.RowNumber, "Slug", $"El slug '{tenant.Slug}' ya existe."));

        if (string.IsNullOrWhiteSpace(tenant.CurrencyCode) || tenant.CurrencyCode.Trim().Length != 3)
            errors.Add(Err(InitialDataSheetNames.Tenant, tenant.RowNumber, "CurrencyCode", "CurrencyCode debe ser ISO de 3 letras (ej. COP)."));
    }

    private static void ValidateBilling(BillingRow? billing, List<TenantInitialDataErrorDto> errors)
    {
        if (billing is null)
        {
            errors.Add(Err(InitialDataSheetNames.Billing, null, null, "Datos de facturación requeridos."));
            return;
        }

        Require(billing.TradeName, InitialDataSheetNames.Billing, billing.RowNumber, "TradeName", errors);
        Require(billing.LegalName, InitialDataSheetNames.Billing, billing.RowNumber, "LegalName", errors);
        Require(billing.TaxId, InitialDataSheetNames.Billing, billing.RowNumber, "TaxId", errors);
        Require(billing.AddressLine, InitialDataSheetNames.Billing, billing.RowNumber, "AddressLine", errors);
        Require(billing.City, InitialDataSheetNames.Billing, billing.RowNumber, "City", errors);

        if (billing.MaxDiscountPercent is < 0 or > 100)
            errors.Add(Err(InitialDataSheetNames.Billing, billing.RowNumber, "MaxDiscountPercent", "Debe estar entre 0 y 100."));

        if (billing.OperationalDayCutoffHour is < 0 or > 23)
            errors.Add(Err(InitialDataSheetNames.Billing, billing.RowNumber, "OperationalDayCutoffHour", "Debe estar entre 0 y 23."));

        if (billing.ImpoconsumoPercent is < 0 or > 100)
            errors.Add(Err(InitialDataSheetNames.Billing, billing.RowNumber, "ImpoconsumoPercent", "Debe estar entre 0 y 100."));
    }

    private static void ValidateProductTypes(List<ProductTypeRow> rows, List<TenantInitialDataErrorDto> errors)
    {
        if (rows.Count == 0)
            errors.Add(Err(InitialDataSheetNames.ProductTypes, null, null, "Debe incluir al menos un tipo de producto."));

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            Require(row.Code, InitialDataSheetNames.ProductTypes, row.RowNumber, "Code", errors);
            Require(row.Name, InitialDataSheetNames.ProductTypes, row.RowNumber, "Name", errors);
            if (!string.IsNullOrWhiteSpace(row.Code) && !codes.Add(row.Code.Trim()))
                errors.Add(Err(InitialDataSheetNames.ProductTypes, row.RowNumber, "Code", $"Código duplicado '{row.Code}'."));
        }
    }

    private static void ValidateProducts(
        List<ProductRow> rows,
        List<ProductTypeRow> productTypes,
        List<TenantInitialDataErrorDto> errors)
    {
        if (rows.Count == 0)
            errors.Add(Err(InitialDataSheetNames.Products, null, null, "Debe incluir al menos un producto."));

        var typeCodes = productTypes
            .Select(p => p.Code.Trim())
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            Require(row.Code, InitialDataSheetNames.Products, row.RowNumber, "Code", errors);
            Require(row.Name, InitialDataSheetNames.Products, row.RowNumber, "Name", errors);
            Require(row.ProductTypeCode, InitialDataSheetNames.Products, row.RowNumber, "ProductTypeCode", errors);

            if (!string.IsNullOrWhiteSpace(row.Code) && !codes.Add(row.Code.Trim()))
                errors.Add(Err(InitialDataSheetNames.Products, row.RowNumber, "Code", $"Código duplicado '{row.Code}'."));

            if (!string.IsNullOrWhiteSpace(row.ProductTypeCode) && !typeCodes.Contains(row.ProductTypeCode.Trim()))
                errors.Add(Err(InitialDataSheetNames.Products, row.RowNumber, "ProductTypeCode", $"Tipo '{row.ProductTypeCode}' no existe en ProductTypes."));

            if (!AllowedCompositionTypes.Contains(row.CompositionType.Trim()))
                errors.Add(Err(InitialDataSheetNames.Products, row.RowNumber, "CompositionType", "Use Prepared, Resale o Bundle."));

            if (row.UnitPrice < 0)
                errors.Add(Err(InitialDataSheetNames.Products, row.RowNumber, "UnitPrice", "UnitPrice no puede ser negativo."));
        }
    }

    private static void ValidateIngredients(List<IngredientRow> rows, List<TenantInitialDataErrorDto> errors)
    {
        if (rows.Count == 0)
            errors.Add(Err(InitialDataSheetNames.Ingredients, null, null, "Debe incluir al menos un ingrediente."));

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            Require(row.Code, InitialDataSheetNames.Ingredients, row.RowNumber, "Code", errors);
            Require(row.Name, InitialDataSheetNames.Ingredients, row.RowNumber, "Name", errors);
            Require(row.Category, InitialDataSheetNames.Ingredients, row.RowNumber, "Category", errors);

            if (!string.IsNullOrWhiteSpace(row.Code) && !codes.Add(row.Code.Trim()))
                errors.Add(Err(InitialDataSheetNames.Ingredients, row.RowNumber, "Code", $"Código duplicado '{row.Code}'."));

            if (!AllowedUnits.Contains(row.Unit.Trim()))
                errors.Add(Err(InitialDataSheetNames.Ingredients, row.RowNumber, "Unit", "Use Unit, Kilogram, Gram, Liter o Milliliter."));

            if (row.UnitCost is < 0)
                errors.Add(Err(InitialDataSheetNames.Ingredients, row.RowNumber, "UnitCost", "UnitCost no puede ser negativo."));

            if (row.StockQuantity is < 0)
                errors.Add(Err(InitialDataSheetNames.Ingredients, row.RowNumber, "StockQuantity", "StockQuantity no puede ser negativo."));
        }
    }

    private static void ValidateRecipes(
        List<RecipeRow> rows,
        List<ProductRow> products,
        List<IngredientRow> ingredients,
        List<TenantInitialDataErrorDto> errors)
    {
        var productCodes = products
            .Select(p => p.Code.Trim())
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ingredientCodes = ingredients
            .Select(i => i.Code.Trim())
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            Require(row.ProductCode, InitialDataSheetNames.Recipes, row.RowNumber, "ProductCode", errors);
            Require(row.IngredientCode, InitialDataSheetNames.Recipes, row.RowNumber, "IngredientCode", errors);

            if (!string.IsNullOrWhiteSpace(row.ProductCode) && !productCodes.Contains(row.ProductCode.Trim()))
                errors.Add(Err(InitialDataSheetNames.Recipes, row.RowNumber, "ProductCode", $"Producto '{row.ProductCode}' no existe en Products."));

            if (!string.IsNullOrWhiteSpace(row.IngredientCode) && !ingredientCodes.Contains(row.IngredientCode.Trim()))
                errors.Add(Err(InitialDataSheetNames.Recipes, row.RowNumber, "IngredientCode", $"Ingrediente '{row.IngredientCode}' no existe en Ingredients."));

            if (row.Quantity <= 0)
                errors.Add(Err(InitialDataSheetNames.Recipes, row.RowNumber, "Quantity", "Quantity debe ser mayor a 0."));

            var key = $"{row.ProductCode.Trim()}|{row.IngredientCode.Trim()}";
            if (!pairs.Add(key))
                errors.Add(Err(InitialDataSheetNames.Recipes, row.RowNumber, null, $"Receta duplicada para '{row.ProductCode}' + '{row.IngredientCode}'."));
        }
    }

    private static void ValidateDiningTables(List<DiningTableRow> rows, List<TenantInitialDataErrorDto> errors)
    {
        if (rows.Count == 0)
            errors.Add(Err(InitialDataSheetNames.DiningTables, null, null, "Debe incluir al menos una mesa."));

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            Require(row.Code, InitialDataSheetNames.DiningTables, row.RowNumber, "Code", errors);
            if (!string.IsNullOrWhiteSpace(row.Code) && !codes.Add(row.Code.Trim()))
                errors.Add(Err(InitialDataSheetNames.DiningTables, row.RowNumber, "Code", $"Código duplicado '{row.Code}'."));

            if (row.Capacity <= 0)
                errors.Add(Err(InitialDataSheetNames.DiningTables, row.RowNumber, "Capacity", "Capacity debe ser mayor a 0."));
        }
    }

    private static void Require(string value, string sheet, int row, string field, List<TenantInitialDataErrorDto> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(Err(sheet, row, field, $"{field} es obligatorio."));
    }

    private static TenantInitialDataErrorDto Err(string sheet, int? row, string? field, string message) =>
        new()
        {
            Sheet = sheet,
            Row = row,
            Field = field,
            Message = message,
        };
}
