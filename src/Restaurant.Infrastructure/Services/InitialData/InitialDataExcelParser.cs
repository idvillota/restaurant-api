using ClosedXML.Excel;
using Restaurant.Application.Features.Platform;

namespace Restaurant.Infrastructure.Services.InitialData;

internal static class InitialDataExcelParser
{
    public static InitialDataWorkbook Parse(Stream stream, out List<TenantInitialDataErrorDto> parseErrors)
    {
        parseErrors = [];
        var workbook = new InitialDataWorkbook();

        using var xl = new XLWorkbook(stream);
        foreach (var required in InitialDataSheetNames.Required)
        {
            if (!xl.Worksheets.TryGetWorksheet(required, out _))
            {
                parseErrors.Add(new TenantInitialDataErrorDto
                {
                    Sheet = required,
                    Message = $"Falta la hoja requerida '{required}'.",
                });
            }
        }

        if (parseErrors.Count > 0)
            return workbook;

        workbook.Tenant = ReadSingleTenant(xl.Worksheet(InitialDataSheetNames.Tenant), parseErrors);
        workbook.Billing = ReadSingleBilling(xl.Worksheet(InitialDataSheetNames.Billing), parseErrors);
        workbook.ProductTypes.AddRange(ReadProductTypes(xl.Worksheet(InitialDataSheetNames.ProductTypes), parseErrors));
        workbook.Products.AddRange(ReadProducts(xl.Worksheet(InitialDataSheetNames.Products), parseErrors));
        workbook.Ingredients.AddRange(ReadIngredients(xl.Worksheet(InitialDataSheetNames.Ingredients), parseErrors));
        workbook.Recipes.AddRange(ReadRecipes(xl.Worksheet(InitialDataSheetNames.Recipes), parseErrors));
        workbook.DiningTables.AddRange(ReadDiningTables(xl.Worksheet(InitialDataSheetNames.DiningTables), parseErrors));

        return workbook;
    }

    public static byte[] BuildTemplate()
    {
        using var xl = new XLWorkbook();

        var tenant = xl.AddWorksheet(InitialDataSheetNames.Tenant);
        WriteHeader(tenant, ["Name", "Slug", "TimeZoneId", "CurrencyCode"]);
        tenant.Cell(2, 1).Value = "Restaurante Demo";
        tenant.Cell(2, 2).Value = "restaurante-demo";
        tenant.Cell(2, 3).Value = "America/Bogota";
        tenant.Cell(2, 4).Value = "COP";

        var billing = xl.AddWorksheet(InitialDataSheetNames.Billing);
        WriteHeader(billing,
        [
            "TradeName", "LegalName", "TaxId", "TaxRegime", "LegalRepresentative",
            "AddressLine", "City", "Country", "PostalCode", "Phone",
            "MaxDiscountPercent", "OperationalDayCutoffHour", "ImpoconsumoPercent",
        ]);
        billing.Cell(2, 1).Value = "Restaurante Demo";
        billing.Cell(2, 2).Value = "Restaurante Demo S.A.S.";
        billing.Cell(2, 3).Value = "900123456-1";
        billing.Cell(2, 4).Value = "Régimen Simplificado";
        billing.Cell(2, 5).Value = "Representante Legal";
        billing.Cell(2, 6).Value = "Calle 10 # 20-30";
        billing.Cell(2, 7).Value = "Bogotá";
        billing.Cell(2, 8).Value = "Colombia";
        billing.Cell(2, 9).Value = "110111";
        billing.Cell(2, 10).Value = "+57 300 000 0000";
        billing.Cell(2, 11).Value = 15;
        billing.Cell(2, 12).Value = 4;
        billing.Cell(2, 13).Value = 8;

        var productTypes = xl.AddWorksheet(InitialDataSheetNames.ProductTypes);
        WriteHeader(productTypes, ["Code", "Name", "Description", "SortOrder"]);
        productTypes.Cell(2, 1).Value = "PLATOS";
        productTypes.Cell(2, 2).Value = "Platos";
        productTypes.Cell(2, 3).Value = "Comidas principales";
        productTypes.Cell(2, 4).Value = 10;
        productTypes.Cell(3, 1).Value = "BEBIDAS";
        productTypes.Cell(3, 2).Value = "Bebidas";
        productTypes.Cell(3, 4).Value = 20;

        var products = xl.AddWorksheet(InitialDataSheetNames.Products);
        WriteHeader(products, ["Code", "Name", "ProductTypeCode", "CompositionType", "UnitPrice", "Description", "IsActive"]);
        products.Cell(2, 1).Value = "PIZZA-MARG";
        products.Cell(2, 2).Value = "Pizza Margarita";
        products.Cell(2, 3).Value = "PLATOS";
        products.Cell(2, 4).Value = "Prepared";
        products.Cell(2, 5).Value = 28000;
        products.Cell(2, 6).Value = "Clásica";
        products.Cell(2, 7).Value = true;
        products.Cell(3, 1).Value = "LIMONADA";
        products.Cell(3, 2).Value = "Limonada";
        products.Cell(3, 3).Value = "BEBIDAS";
        products.Cell(3, 4).Value = "Prepared";
        products.Cell(3, 5).Value = 8000;
        products.Cell(3, 7).Value = true;

        var ingredients = xl.AddWorksheet(InitialDataSheetNames.Ingredients);
        WriteHeader(ingredients, ["Code", "Name", "Category", "Unit", "UnitCost", "StockQuantity", "ReorderLevel", "IsActive"]);
        ingredients.Cell(2, 1).Value = "TOMATE";
        ingredients.Cell(2, 2).Value = "Tomate";
        ingredients.Cell(2, 3).Value = "Verduras";
        ingredients.Cell(2, 4).Value = "Kilogram";
        ingredients.Cell(2, 5).Value = 3000;
        ingredients.Cell(2, 6).Value = 10;
        ingredients.Cell(2, 7).Value = 2;
        ingredients.Cell(2, 8).Value = true;
        ingredients.Cell(3, 1).Value = "QUESO";
        ingredients.Cell(3, 2).Value = "Queso mozzarella";
        ingredients.Cell(3, 3).Value = "Lácteos";
        ingredients.Cell(3, 4).Value = "Kilogram";
        ingredients.Cell(3, 5).Value = 18000;
        ingredients.Cell(3, 6).Value = 5;
        ingredients.Cell(3, 7).Value = 1;
        ingredients.Cell(3, 8).Value = true;
        ingredients.Cell(4, 1).Value = "LIMON";
        ingredients.Cell(4, 2).Value = "Limón";
        ingredients.Cell(4, 3).Value = "Frutas";
        ingredients.Cell(4, 4).Value = "Unit";
        ingredients.Cell(4, 5).Value = 300;
        ingredients.Cell(4, 6).Value = 50;
        ingredients.Cell(4, 7).Value = 10;
        ingredients.Cell(4, 8).Value = true;
        ingredients.Cell(5, 1).Value = "AZUCAR";
        ingredients.Cell(5, 2).Value = "Azúcar";
        ingredients.Cell(5, 3).Value = "Insumos";
        ingredients.Cell(5, 4).Value = "Kilogram";
        ingredients.Cell(5, 5).Value = 4000;
        ingredients.Cell(5, 6).Value = 8;
        ingredients.Cell(5, 7).Value = 2;
        ingredients.Cell(5, 8).Value = true;

        var recipes = xl.AddWorksheet(InitialDataSheetNames.Recipes);
        WriteHeader(recipes, ["ProductCode", "IngredientCode", "Quantity"]);
        recipes.Cell(2, 1).Value = "PIZZA-MARG";
        recipes.Cell(2, 2).Value = "TOMATE";
        recipes.Cell(2, 3).Value = 0.15;
        recipes.Cell(3, 1).Value = "PIZZA-MARG";
        recipes.Cell(3, 2).Value = "QUESO";
        recipes.Cell(3, 3).Value = 0.2;
        recipes.Cell(4, 1).Value = "LIMONADA";
        recipes.Cell(4, 2).Value = "LIMON";
        recipes.Cell(4, 3).Value = 2;
        recipes.Cell(5, 1).Value = "LIMONADA";
        recipes.Cell(5, 2).Value = "AZUCAR";
        recipes.Cell(5, 3).Value = 0.05;

        var tables = xl.AddWorksheet(InitialDataSheetNames.DiningTables);
        WriteHeader(tables, ["Code", "Capacity", "Zone", "LayoutX", "LayoutY"]);
        tables.Cell(2, 1).Value = "M-01";
        tables.Cell(2, 2).Value = 4;
        tables.Cell(2, 3).Value = "Salón";
        tables.Cell(2, 4).Value = 20;
        tables.Cell(2, 5).Value = 30;
        tables.Cell(3, 1).Value = "M-02";
        tables.Cell(3, 2).Value = 2;
        tables.Cell(3, 3).Value = "Terraza";
        tables.Cell(3, 4).Value = 40;
        tables.Cell(3, 5).Value = 50;

        using var ms = new MemoryStream();
        xl.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;
    }

    private static TenantRow? ReadSingleTenant(IXLWorksheet sheet, List<TenantInitialDataErrorDto> errors)
    {
        var map = HeaderMap(sheet);
        RequireHeaders(InitialDataSheetNames.Tenant, map, ["Name", "Slug"], errors);
        var rows = DataRows(sheet).ToList();
        if (rows.Count == 0)
        {
            errors.Add(Error(InitialDataSheetNames.Tenant, null, null, "Debe haber exactamente 1 fila de datos."));
            return null;
        }

        if (rows.Count > 1)
            errors.Add(Error(InitialDataSheetNames.Tenant, null, null, "Solo se permite 1 fila de datos."));

        var row = rows[0];
        return new TenantRow
        {
            RowNumber = row.RowNumber(),
            Name = Cell(row, map, "Name"),
            Slug = Cell(row, map, "Slug"),
            TimeZoneId = OptionalCell(row, map, "TimeZoneId"),
            CurrencyCode = OptionalCell(row, map, "CurrencyCode") ?? "COP",
        };
    }

    private static BillingRow? ReadSingleBilling(IXLWorksheet sheet, List<TenantInitialDataErrorDto> errors)
    {
        var map = HeaderMap(sheet);
        RequireHeaders(InitialDataSheetNames.Billing, map, ["TradeName", "LegalName", "TaxId", "AddressLine", "City"], errors);
        var rows = DataRows(sheet).ToList();
        if (rows.Count == 0)
        {
            errors.Add(Error(InitialDataSheetNames.Billing, null, null, "Debe haber exactamente 1 fila de datos."));
            return null;
        }

        if (rows.Count > 1)
            errors.Add(Error(InitialDataSheetNames.Billing, null, null, "Solo se permite 1 fila de datos."));

        var row = rows[0];
        return new BillingRow
        {
            RowNumber = row.RowNumber(),
            TradeName = Cell(row, map, "TradeName"),
            LegalName = Cell(row, map, "LegalName"),
            TaxId = Cell(row, map, "TaxId"),
            TaxRegime = OptionalCell(row, map, "TaxRegime") ?? "Régimen Simplificado",
            LegalRepresentative = OptionalCell(row, map, "LegalRepresentative"),
            AddressLine = Cell(row, map, "AddressLine"),
            City = Cell(row, map, "City"),
            Country = OptionalCell(row, map, "Country") ?? "Colombia",
            PostalCode = OptionalCell(row, map, "PostalCode"),
            Phone = OptionalCell(row, map, "Phone"),
            MaxDiscountPercent = ParseDecimal(row, map, "MaxDiscountPercent", 15m, errors, InitialDataSheetNames.Billing),
            OperationalDayCutoffHour = ParseInt(row, map, "OperationalDayCutoffHour", 4, errors, InitialDataSheetNames.Billing),
            ImpoconsumoPercent = ParseDecimal(row, map, "ImpoconsumoPercent", 8m, errors, InitialDataSheetNames.Billing),
        };
    }

    private static IEnumerable<ProductTypeRow> ReadProductTypes(IXLWorksheet sheet, List<TenantInitialDataErrorDto> errors)
    {
        var map = HeaderMap(sheet);
        RequireHeaders(InitialDataSheetNames.ProductTypes, map, ["Code", "Name"], errors);
        foreach (var row in DataRows(sheet))
        {
            yield return new ProductTypeRow
            {
                RowNumber = row.RowNumber(),
                Code = Cell(row, map, "Code"),
                Name = Cell(row, map, "Name"),
                Description = OptionalCell(row, map, "Description"),
                SortOrder = ParseInt(row, map, "SortOrder", 0, errors, InitialDataSheetNames.ProductTypes),
            };
        }
    }

    private static IEnumerable<ProductRow> ReadProducts(IXLWorksheet sheet, List<TenantInitialDataErrorDto> errors)
    {
        var map = HeaderMap(sheet);
        RequireHeaders(InitialDataSheetNames.Products, map, ["Code", "Name", "ProductTypeCode", "UnitPrice"], errors);
        foreach (var row in DataRows(sheet))
        {
            yield return new ProductRow
            {
                RowNumber = row.RowNumber(),
                Code = Cell(row, map, "Code"),
                Name = Cell(row, map, "Name"),
                ProductTypeCode = Cell(row, map, "ProductTypeCode"),
                CompositionType = OptionalCell(row, map, "CompositionType") ?? "Prepared",
                UnitPrice = ParseDecimal(row, map, "UnitPrice", 0m, errors, InitialDataSheetNames.Products),
                Description = OptionalCell(row, map, "Description"),
                IsActive = ParseBool(row, map, "IsActive", true, errors, InitialDataSheetNames.Products),
            };
        }
    }

    private static IEnumerable<IngredientRow> ReadIngredients(IXLWorksheet sheet, List<TenantInitialDataErrorDto> errors)
    {
        var map = HeaderMap(sheet);
        RequireHeaders(InitialDataSheetNames.Ingredients, map, ["Code", "Name", "Category", "Unit"], errors);
        foreach (var row in DataRows(sheet))
        {
            yield return new IngredientRow
            {
                RowNumber = row.RowNumber(),
                Code = Cell(row, map, "Code"),
                Name = Cell(row, map, "Name"),
                Category = Cell(row, map, "Category"),
                Unit = OptionalCell(row, map, "Unit") ?? "Unit",
                UnitCost = ParseOptionalDecimal(row, map, "UnitCost", errors, InitialDataSheetNames.Ingredients),
                StockQuantity = ParseOptionalDecimal(row, map, "StockQuantity", errors, InitialDataSheetNames.Ingredients),
                ReorderLevel = ParseOptionalDecimal(row, map, "ReorderLevel", errors, InitialDataSheetNames.Ingredients),
                IsActive = ParseBool(row, map, "IsActive", true, errors, InitialDataSheetNames.Ingredients),
            };
        }
    }

    private static IEnumerable<RecipeRow> ReadRecipes(IXLWorksheet sheet, List<TenantInitialDataErrorDto> errors)
    {
        var map = HeaderMap(sheet);
        RequireHeaders(InitialDataSheetNames.Recipes, map, ["ProductCode", "IngredientCode", "Quantity"], errors);
        foreach (var row in DataRows(sheet))
        {
            yield return new RecipeRow
            {
                RowNumber = row.RowNumber(),
                ProductCode = Cell(row, map, "ProductCode"),
                IngredientCode = Cell(row, map, "IngredientCode"),
                Quantity = ParseDecimal(row, map, "Quantity", 0m, errors, InitialDataSheetNames.Recipes),
            };
        }
    }

    private static IEnumerable<DiningTableRow> ReadDiningTables(IXLWorksheet sheet, List<TenantInitialDataErrorDto> errors)
    {
        var map = HeaderMap(sheet);
        RequireHeaders(InitialDataSheetNames.DiningTables, map, ["Code", "Capacity"], errors);
        foreach (var row in DataRows(sheet))
        {
            yield return new DiningTableRow
            {
                RowNumber = row.RowNumber(),
                Code = Cell(row, map, "Code"),
                Capacity = ParseInt(row, map, "Capacity", 0, errors, InitialDataSheetNames.DiningTables),
                Zone = OptionalCell(row, map, "Zone"),
                LayoutX = ParseOptionalDouble(row, map, "LayoutX", errors, InitialDataSheetNames.DiningTables),
                LayoutY = ParseOptionalDouble(row, map, "LayoutY", errors, InitialDataSheetNames.DiningTables),
            };
        }
    }

    private static Dictionary<string, int> HeaderMap(IXLWorksheet sheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var c = 1; c <= lastCol; c++)
        {
            var name = sheet.Cell(1, c).GetString().Trim();
            if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
                map[name] = c;
        }

        return map;
    }

    private static void RequireHeaders(
        string sheetName,
        Dictionary<string, int> map,
        IEnumerable<string> required,
        List<TenantInitialDataErrorDto> errors)
    {
        foreach (var header in required)
        {
            if (!map.ContainsKey(header))
            {
                errors.Add(Error(sheetName, 1, header, $"Falta la columna requerida '{header}'."));
            }
        }
    }

    private static IEnumerable<IXLRow> DataRows(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            if (row.IsEmpty())
                continue;
            yield return row;
        }
    }

    private static string Cell(IXLRow row, Dictionary<string, int> map, string header)
    {
        if (!map.TryGetValue(header, out var col))
            return string.Empty;
        return row.Cell(col).GetString().Trim();
    }

    private static string? OptionalCell(IXLRow row, Dictionary<string, int> map, string header)
    {
        if (!map.TryGetValue(header, out var col))
            return null;
        var value = row.Cell(col).GetString().Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static decimal ParseDecimal(
        IXLRow row,
        Dictionary<string, int> map,
        string header,
        decimal defaultValue,
        List<TenantInitialDataErrorDto> errors,
        string sheet)
    {
        if (!map.TryGetValue(header, out var col))
            return defaultValue;

        var cell = row.Cell(col);
        if (cell.IsEmpty())
            return defaultValue;

        if (cell.TryGetValue(out double d))
            return (decimal)d;

        var text = cell.GetString().Trim();
        if (string.IsNullOrEmpty(text))
            return defaultValue;

        if (decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        errors.Add(Error(sheet, row.RowNumber(), header, $"Valor numérico inválido: '{text}'."));
        return defaultValue;
    }

    private static decimal? ParseOptionalDecimal(
        IXLRow row,
        Dictionary<string, int> map,
        string header,
        List<TenantInitialDataErrorDto> errors,
        string sheet)
    {
        if (!map.TryGetValue(header, out var col))
            return null;

        var cell = row.Cell(col);
        if (cell.IsEmpty())
            return null;

        if (cell.TryGetValue(out double d))
            return (decimal)d;

        var text = cell.GetString().Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        if (decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        errors.Add(Error(sheet, row.RowNumber(), header, $"Valor numérico inválido: '{text}'."));
        return null;
    }

    private static double? ParseOptionalDouble(
        IXLRow row,
        Dictionary<string, int> map,
        string header,
        List<TenantInitialDataErrorDto> errors,
        string sheet)
    {
        if (!map.TryGetValue(header, out var col))
            return null;

        var cell = row.Cell(col);
        if (cell.IsEmpty())
            return null;

        if (cell.TryGetValue(out double d))
            return d;

        var text = cell.GetString().Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        if (double.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        errors.Add(Error(sheet, row.RowNumber(), header, $"Valor numérico inválido: '{text}'."));
        return null;
    }

    private static int ParseInt(
        IXLRow row,
        Dictionary<string, int> map,
        string header,
        int defaultValue,
        List<TenantInitialDataErrorDto> errors,
        string sheet)
    {
        if (!map.TryGetValue(header, out var col))
            return defaultValue;

        var cell = row.Cell(col);
        if (cell.IsEmpty())
            return defaultValue;

        if (cell.TryGetValue(out double d))
            return (int)d;

        var text = cell.GetString().Trim();
        if (string.IsNullOrEmpty(text))
            return defaultValue;

        if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        errors.Add(Error(sheet, row.RowNumber(), header, $"Valor entero inválido: '{text}'."));
        return defaultValue;
    }

    private static bool ParseBool(
        IXLRow row,
        Dictionary<string, int> map,
        string header,
        bool defaultValue,
        List<TenantInitialDataErrorDto> errors,
        string sheet)
    {
        if (!map.TryGetValue(header, out var col))
            return defaultValue;

        var cell = row.Cell(col);
        if (cell.IsEmpty())
            return defaultValue;

        if (cell.TryGetValue(out bool b))
            return b;

        var text = cell.GetString().Trim();
        if (string.IsNullOrEmpty(text))
            return defaultValue;

        if (bool.TryParse(text, out var parsed))
            return parsed;

        var lower = text.ToLowerInvariant();
        if (lower is "1" or "si" or "sí" or "yes" or "y")
            return true;
        if (lower is "0" or "no" or "n")
            return false;

        errors.Add(Error(sheet, row.RowNumber(), header, $"Valor booleano inválido: '{text}'. Use true/false."));
        return defaultValue;
    }

    private static TenantInitialDataErrorDto Error(string sheet, int? row, string? field, string message) =>
        new()
        {
            Sheet = sheet,
            Row = row,
            Field = field,
            Message = message,
        };
}
