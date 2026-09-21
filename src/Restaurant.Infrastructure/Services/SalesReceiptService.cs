using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Features.Sales.SalesReceipts;
using Restaurant.Domain.Entities;
using Restaurant.Domain.Enums;
using Restaurant.Infrastructure.Persistence;
using Restaurant.Infrastructure.SalesReceipts;

namespace Restaurant.Infrastructure.Services;

public sealed class SalesReceiptService : ISalesReceiptService
{
    static SalesReceiptService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IGeneratedFileStorage _fileStorage;

    public SalesReceiptService(
        ApplicationDbContext db,
        ICurrentTenantContext tenantContext,
        IGeneratedFileStorage fileStorage)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fileStorage = fileStorage;
    }

    public async Task<SalesReceiptModel> BuildModelAsync(
        Bill bill,
        TenantSettings settings,
        CancellationToken cancellationToken = default)
    {
        var lines = await _db.BillLines
            .AsNoTracking()
            .Where(l => l.BillId == bill.Id)
            .OrderBy(l => l.CreatedAtUtc)
            .ThenBy(l => l.ProductName)
            .ToListAsync(cancellationToken);

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstAsync(c => c.Id == bill.CustomerId, cancellationToken);

        var tenantEntity = await _db.Tenants.AsNoTracking().FirstAsync(t => t.Id == bill.TenantId, cancellationToken);

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.BillId == bill.Id)
            .ToListAsync(cancellationToken);

        var amountTendered = payments.Sum(p => p.Amount);
        var changeDue = Math.Max(0, amountTendered - bill.Total);
        // Prices are tax-included; show the extracted base (without impoconsumo) on the receipt.
        var discountedGross = Math.Max(0, bill.Subtotal - bill.DiscountAmount);
        var impoconsumoBase =
            settings.ImpoconsumoPercent > 0 && discountedGross > 0
                ? decimal.Round(
                    discountedGross / (1m + (settings.ImpoconsumoPercent / 100m)),
                    2,
                    MidpointRounding.AwayFromZero)
                : discountedGross;
        var articleCount = (int)lines.Sum(l => l.Quantity);

        return new SalesReceiptModel
        {
            Tenant = MapTenant(settings),
            InvoiceDisplayNumber = BillCheckoutCalculator.FormatInvoiceDisplayNumber(
                settings.InvoiceNumberPrefix,
                bill.DianConsecutiveNumber),
            DianConsecutiveNumber = bill.DianConsecutiveNumber,
            BillNumber = bill.Number,
            IssuedAtUtc = bill.PaidAtUtc ?? bill.IssuedAtUtc,
            TableCodes = bill.TableCodesSnapshot,
            OrderNumbers = bill.OrderNumbersSnapshot,
            CustomerName = customer.Name,
            CustomerTaxId = customer.TaxId,
            CashierName = bill.ProcessedByDisplayName,
            Lines = lines.Select(l => new SalesReceiptLineModel
            {
                ProductName = l.ProductName,
                ProductTypeName = l.ProductTypeName,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
                ImpoconsumoAmount = l.ImpoconsumoAmount,
                Notes = l.Notes,
            }).ToList(),
            Payments = payments
                .Select(p => new SalesReceiptPaymentModel
                {
                    MethodLabel = PaymentMethodLabel(p.Method),
                    Amount = p.Amount,
                })
                .ToList(),
            Subtotal = bill.Subtotal,
            DiscountAmount = bill.DiscountAmount,
            DiscountPercent = bill.DiscountPercent,
            ImpoconsumoPercent = settings.ImpoconsumoPercent,
            ImpoconsumoAmount = bill.TaxAmount,
            ImpoconsumoBase = impoconsumoBase,
            TipAmount = bill.TipAmount,
            Total = bill.Total,
            ArticleCount = articleCount,
            AmountTendered = amountTendered,
            ChangeDue = changeDue,
            CurrencyCode = tenantEntity.CurrencyCode,
        };
    }

    public async Task<SalesReceiptFilesDto> GenerateFilesAsync(
        SalesReceiptModel model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantFolder = _tenantContext.TenantId?.ToString("N") ?? "shared";
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var safeInvoice = SanitizeFileToken(model.InvoiceDisplayNumber);
        var baseName = $"factura_{stamp}_{safeInvoice}_{Guid.NewGuid():N}";

        var pdfRelativePath = await _fileStorage.SaveAsync(
            $"receipts/{tenantFolder}/{baseName}.pdf",
            QuestPdfSalesReceiptDocument.BuildPdf(model),
            "application/pdf",
            cancellationToken);

        var xmlRelativePath = await _fileStorage.SaveAsync(
            $"receipts/{tenantFolder}/{baseName}.xml",
            SalesReceiptXmlBuilder.BuildXml(model),
            "application/xml",
            cancellationToken);

        return new SalesReceiptFilesDto
        {
            PdfRelativePath = pdfRelativePath,
            XmlRelativePath = xmlRelativePath,
        };
    }

    private static SalesReceiptTenantInfo MapTenant(TenantSettings settings) =>
        new()
        {
            TradeName = settings.TradeName,
            LegalName = settings.LegalName,
            TaxRegime = settings.TaxRegime,
            TaxId = settings.TaxId,
            LegalRepresentative = settings.LegalRepresentative,
            AddressLine = settings.AddressLine,
            City = settings.City,
            Country = settings.Country,
            PostalCode = settings.PostalCode,
            Phone = settings.Phone,
            DianResolutionNumber = settings.DianResolutionNumber,
            DianResolutionFrom = settings.DianResolutionFrom,
            DianResolutionTo = settings.DianResolutionTo,
            InvoiceNumberPrefix = settings.InvoiceNumberPrefix,
        };

    private static string PaymentMethodLabel(PaymentMethod method) =>
        method switch
        {
            PaymentMethod.Cash => "EFECTIVO",
            PaymentMethod.Card => "TARJETA",
            PaymentMethod.Transfer => "TRANSFERENCIA",
            _ => "OTRO",
        };

    private static string SanitizeFileToken(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return "factura";

        return new string(trimmed.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    }
}
