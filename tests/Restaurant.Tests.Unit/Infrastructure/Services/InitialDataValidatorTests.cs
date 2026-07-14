using Restaurant.Infrastructure.Services.InitialData;

namespace Restaurant.Tests.Unit.Infrastructure.Services;

public sealed class InitialDataValidatorTests
{
    [Fact]
    public void Validate_HappyPath_ReturnsNoErrors()
    {
        var workbook = SampleWorkbook();
        var errors = InitialDataValidator.Validate(workbook, slugAlreadyExists: false);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingProductTypeRef_ReturnsError()
    {
        var workbook = SampleWorkbook();
        workbook.Products[0] = new ProductRow
        {
            RowNumber = 2,
            Code = "PIZZA",
            Name = "Pizza",
            ProductTypeCode = "MISSING",
            CompositionType = "Prepared",
            UnitPrice = 10,
            IsActive = true,
        };

        var errors = InitialDataValidator.Validate(workbook, slugAlreadyExists: false);
        Assert.Contains(errors, e => e.Sheet == "Products" && e.Field == "ProductTypeCode");
    }

    [Fact]
    public void Validate_DuplicateSlug_WhenExists_ReturnsError()
    {
        var workbook = SampleWorkbook();
        var errors = InitialDataValidator.Validate(workbook, slugAlreadyExists: true);
        Assert.Contains(errors, e => e.Sheet == "Tenant" && e.Field == "Slug");
    }

    [Fact]
    public void Validate_InvalidRecipeQuantity_ReturnsError()
    {
        var workbook = SampleWorkbook();
        workbook.Recipes[0] = new RecipeRow
        {
            RowNumber = 2,
            ProductCode = "PIZZA",
            IngredientCode = "TOMATE",
            Quantity = 0,
        };

        var errors = InitialDataValidator.Validate(workbook, slugAlreadyExists: false);
        Assert.Contains(errors, e => e.Sheet == "Recipes" && e.Field == "Quantity");
    }

    [Fact]
    public void BuildTemplate_ContainsRequiredSheets()
    {
        var bytes = InitialDataExcelParser.BuildTemplate();
        Assert.True(bytes.Length > 0);

        using var stream = new MemoryStream(bytes);
        var workbook = InitialDataExcelParser.Parse(stream, out var parseErrors);
        Assert.Empty(parseErrors);
        Assert.NotNull(workbook.Tenant);
        Assert.NotNull(workbook.Billing);
        Assert.NotEmpty(workbook.Products);
        Assert.NotEmpty(workbook.Ingredients);
        Assert.NotEmpty(workbook.Recipes);
        Assert.NotEmpty(workbook.DiningTables);
    }

    private static InitialDataWorkbook SampleWorkbook()
    {
        var workbook = new InitialDataWorkbook
        {
            Tenant = new TenantRow
            {
                RowNumber = 2,
                Name = "Demo",
                Slug = "demo-local",
                TimeZoneId = "America/Bogota",
                CurrencyCode = "COP",
            },
            Billing = new BillingRow
            {
                RowNumber = 2,
                TradeName = "Demo",
                LegalName = "Demo SAS",
                TaxId = "900",
                AddressLine = "Calle 1",
                City = "Bogotá",
                Country = "Colombia",
                MaxDiscountPercent = 15,
                OperationalDayCutoffHour = 4,
                ImpoconsumoPercent = 8,
            },
        };

        workbook.ProductTypes.Add(new ProductTypeRow
        {
            RowNumber = 2,
            Code = "PLATOS",
            Name = "Platos",
            SortOrder = 10,
        });
        workbook.Products.Add(new ProductRow
        {
            RowNumber = 2,
            Code = "PIZZA",
            Name = "Pizza",
            ProductTypeCode = "PLATOS",
            CompositionType = "Prepared",
            UnitPrice = 20000,
            IsActive = true,
        });
        workbook.Ingredients.Add(new IngredientRow
        {
            RowNumber = 2,
            Code = "TOMATE",
            Name = "Tomate",
            Category = "Verduras",
            Unit = "Kilogram",
            IsActive = true,
        });
        workbook.Recipes.Add(new RecipeRow
        {
            RowNumber = 2,
            ProductCode = "PIZZA",
            IngredientCode = "TOMATE",
            Quantity = 0.2m,
        });
        workbook.DiningTables.Add(new DiningTableRow
        {
            RowNumber = 2,
            Code = "M-01",
            Capacity = 4,
            Zone = "Salón",
        });

        return workbook;
    }
}
