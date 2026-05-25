using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using Restaurant.Application.Common.Interfaces;
using Restaurant.Application.Common.Options;
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
    private readonly string _absoluteRoot;

    public SalesReceiptService(
        ApplicationDbContext db,
        ICurrentTenantContext tenantContext,
        IOptions<SalesReceiptOptions> options,
        IHostEnvironment environment)
    {
        _db = db;
        _tenantContext = tenantContext;

        var rootPath = options.Value.RootPath.Trim().TrimEnd('/', '\\');
        _absoluteRoot = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.Combine(environment.ContentRootPath, rootPath);
        Directory.CreateDirectory(_absoluteRoot);
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
        var impoconsumoBase = Math.Max(0, bill.Subtotal - bill.DiscountAmount);
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

    public Task<SalesReceiptFilesDto> GenerateFilesAsync(
        SalesReceiptModel model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantFolder = _tenantContext.TenantId?.ToString("N") ?? "shared";
        var tenantRoot = Path.Combine(_absoluteRoot, tenantFolder);
        Directory.CreateDirectory(tenantRoot);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var safeInvoice = SanitizeFileToken(model.InvoiceDisplayNumber);
        var baseName = $"factura_{stamp}_{safeInvoice}_{Guid.NewGuid():N}";

        var pdfPath = Path.Combine(tenantRoot, $"{baseName}.pdf");
        var xmlPath = Path.Combine(tenantRoot, $"{baseName}.xml");

        File.WriteAllBytes(pdfPath, QuestPdfSalesReceiptDocument.BuildPdf(model));
        File.WriteAllBytes(xmlPath, SalesReceiptXmlBuilder.BuildXml(model));

        return Task.FromResult(
            new SalesReceiptFilesDto
            {
                PdfRelativePath = Path.Combine(tenantFolder, $"{baseName}.pdf").Replace('\\', '/'),
                XmlRelativePath = Path.Combine(tenantFolder, $"{baseName}.xml").Replace('\\', '/'),
            });
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
