using QuestPDF.Infrastructure;
using Restaurant.Application.Features.Sales.SalesReceipts;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.SalesReceipts;

namespace Restaurant.Tests.Unit.Infrastructure.SalesReceipts;

public sealed class QuestPdfSalesReceiptDocumentTests
{
    static QuestPdfSalesReceiptDocumentTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Theory]
    [InlineData(58)]
    [InlineData(80)]
    public void BuildPdf_succeeds_for_configured_thermal_paper_width(int paperWidthMm)
    {
        var model = CreateSampleReceipt(paperWidthMm);

        var pdf = QuestPdfSalesReceiptDocument.BuildPdf(model);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 200, $"Expected a non-empty PDF for {paperWidthMm}mm");
        Assert.Equal(0x25, pdf[0]); // %
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    [Theory]
    [InlineData(58)]
    [InlineData(80)]
    public void BuildPdf_includes_tenant_logo_when_bytes_present(int paperWidthMm)
    {
        var model = CreateSampleReceipt(paperWidthMm);
        model.LogoImageBytes = TinyPng;

        var withLogo = QuestPdfSalesReceiptDocument.BuildPdf(model);
        model.LogoImageBytes = null;
        var withoutLogo = QuestPdfSalesReceiptDocument.BuildPdf(model);

        Assert.True(withLogo.Length > withoutLogo.Length);
    }

    [Fact]
    public void BuildPdf_succeeds_when_trade_and_legal_name_match()
    {
        var model = CreateSampleReceipt(58);
        model.Tenant.TradeName = "Raiz restaurante";
        model.Tenant.LegalName = "Raiz restaurante";
        model.CashierName = "Iván Villota";

        var pdf = QuestPdfSalesReceiptDocument.BuildPdf(model);

        Assert.True(pdf.Length > 200);
    }

    [Fact]
    public void BuildPdf_narrow_layout_handles_long_money_and_notes()
    {
        var model = CreateSampleReceipt(58);
        model.Lines =
        [
            new SalesReceiptLineModel
            {
                ProductName = "Café con leche de almendras extra grande",
                ProductTypeName = "Bebidas",
                Quantity = 12,
                UnitPrice = 18500m,
                LineTotal = 222000m,
                Notes = "Sin azúcar, temperatura media, vaso para llevar",
            },
            new SalesReceiptLineModel
            {
                ProductName = "Croissant de mantequilla",
                ProductTypeName = "Panadería",
                Quantity = 3,
                UnitPrice = 6500m,
                LineTotal = 19500m,
            },
        ];
        model.Total = 241500m;
        model.Payments =
        [
            new SalesReceiptPaymentModel { MethodLabel = "EFECTIVO", Amount = 250000m },
        ];
        model.ChangeDue = 8500m;
        model.TipAmount = 5000m;
        model.DiscountAmount = 2000m;
        model.ArticleCount = 15;

        var pdf = QuestPdfSalesReceiptDocument.BuildPdf(model);

        Assert.True(pdf.Length > 200);
    }

    // 1×1 PNG
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static SalesReceiptModel CreateSampleReceipt(int paperWidthMm) =>
        new()
        {
            PaperWidthMm = (int)ThermalPaperWidth.Normalize(paperWidthMm),
            InvoiceDisplayNumber = "FV-1001",
            DianConsecutiveNumber = 1001,
            BillNumber = "BILL-0001",
            IssuedAtUtc = DateTime.UtcNow,
            TableCodes = "A1",
            CustomerName = "Consumidor final",
            CustomerTaxId = "2222222",
            CashierName = "Cajero",
            ImpoconsumoPercent = 8m,
            ImpoconsumoBase = 9259.26m,
            ImpoconsumoAmount = 740.74m,
            Subtotal = 10000m,
            Total = 10000m,
            ArticleCount = 1,
            AmountTendered = 10000m,
            Lines =
            [
                new SalesReceiptLineModel
                {
                    ProductName = "Café americano",
                    ProductTypeName = "Bebidas",
                    Quantity = 1,
                    UnitPrice = 10000m,
                    LineTotal = 10000m,
                },
            ],
            Payments =
            [
                new SalesReceiptPaymentModel { MethodLabel = "EFECTIVO", Amount = 10000m },
            ],
            Tenant = new SalesReceiptTenantInfo
            {
                TradeName = "Nuestro Café",
                LegalName = "Nuestro Café SAS",
                TaxId = "900123456-1",
                AddressLine = "Calle 1 # 2-3",
                Phone = "3001234567",
                TaxRegime = "Régimen simple",
                DianResolutionNumber = "18764000000000",
                DianResolutionFrom = 1,
                DianResolutionTo = 5000,
                InvoiceNumberPrefix = "FV",
            },
        };
}
