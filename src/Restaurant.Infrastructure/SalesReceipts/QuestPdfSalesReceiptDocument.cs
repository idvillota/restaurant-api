using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Restaurant.Application.Features.Sales.SalesReceipts;

namespace Restaurant.Infrastructure.SalesReceipts;

internal static class QuestPdfSalesReceiptDocument
{
    private const float TicketWidthMm = 80f;

    public static byte[] BuildPdf(SalesReceiptModel receipt)
    {
        var issuedLocal = receipt.IssuedAtUtc.ToLocalTime();
        var tenant = receipt.Tenant;

        return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.ContinuousSize(TicketWidthMm, Unit.Millimetre);
                    page.MarginHorizontal(3, Unit.Millimetre);
                    page.MarginVertical(4, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Content().Column(column =>
                    {
                        column.Spacing(2);

                        if (!string.IsNullOrWhiteSpace(tenant.TradeName))
                            column.Item().AlignCenter().Text(tenant.TradeName).Bold().FontSize(10);

                        if (!string.IsNullOrWhiteSpace(tenant.TaxRegime))
                            column.Item().AlignCenter().Text(tenant.TaxRegime).FontSize(7);

                        if (!string.IsNullOrWhiteSpace(tenant.LegalName))
                            column.Item().AlignCenter().Text(tenant.LegalName).FontSize(8);

                        if (!string.IsNullOrWhiteSpace(tenant.AddressLine))
                            column.Item().AlignCenter().Text(tenant.AddressLine).FontSize(7);

                        var cityLine = string.Join(
                            ", ",
                            new[] { tenant.City, tenant.TaxId, tenant.LegalRepresentative }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        if (!string.IsNullOrWhiteSpace(cityLine))
                            column.Item().AlignCenter().Text(cityLine).FontSize(7);

                        if (!string.IsNullOrWhiteSpace(tenant.Country) || !string.IsNullOrWhiteSpace(tenant.PostalCode))
                        {
                            column.Item()
                                .AlignCenter()
                                .Text($"{tenant.Country}{(string.IsNullOrWhiteSpace(tenant.PostalCode) ? "" : $", {tenant.PostalCode}")}")
                                .FontSize(7);
                        }

                        if (!string.IsNullOrWhiteSpace(tenant.Phone))
                            column.Item().AlignCenter().Text($"Tel: {tenant.Phone}").FontSize(7);

                        column.Item().PaddingTop(4).LineHorizontal(0.5f);

                        column.Item()
                            .Text($"Impreso {issuedLocal:dd 'de' MMMM 'de' yyyy, h:mm tt}")
                            .FontSize(7);

                        if (!string.IsNullOrWhiteSpace(tenant.DianResolutionNumber))
                        {
                            column.Item()
                                .Text($"Resolución DIAN No. {tenant.DianResolutionNumber}")
                                .FontSize(7);
                            column.Item()
                                .Text(
                                    $"Autorizado del {tenant.DianResolutionFrom} al {tenant.DianResolutionTo}")
                                .FontSize(7);
                        }

                        column.Item()
                            .PaddingTop(2)
                            .Text($"Factura de Venta # {receipt.InvoiceDisplayNumber}")
                            .Bold()
                            .FontSize(9);

                        column.Item().PaddingTop(4).LineHorizontal(0.5f);

                        column.Item().Text($"{issuedLocal:dd 'de' MMMM 'de' yyyy, h:mm tt}").FontSize(7);

                        if (!string.IsNullOrWhiteSpace(receipt.OrderNumbers))
                            column.Item().Text($"Pedido #: {receipt.OrderNumbers}").FontSize(7);

                        if (!string.IsNullOrWhiteSpace(receipt.TableCodes))
                            column.Item().Text($"Mesa: {receipt.TableCodes}").FontSize(7);

                        column.Item().Text($"Cuenta #: {receipt.BillNumber}").FontSize(7);
                        column.Item().Text($"Cliente: {receipt.CustomerName}").FontSize(7);

                        if (!string.IsNullOrWhiteSpace(receipt.CustomerTaxId))
                            column.Item().Text($"NIT/CC: {receipt.CustomerTaxId}").FontSize(7);

                        if (!string.IsNullOrWhiteSpace(receipt.CashierName))
                            column.Item().Text($"Cajero: {receipt.CashierName}").FontSize(7);

                        column.Item().PaddingTop(4).LineHorizontal(0.5f);

                        foreach (var line in receipt.Lines)
                        {
                            column.Item().PaddingTop(3).Row(row =>
                            {
                                row.RelativeItem().Text(line.ProductName).FontSize(8);
                                row.ConstantItem(72).AlignRight().Text(ColombianMoneyFormat.Format(line.LineTotal)).FontSize(8);
                            });

                            if (!string.IsNullOrWhiteSpace(line.Notes))
                            {
                                column.Item()
                                    .PaddingLeft(4)
                                    .Text(line.Notes)
                                    .Italic()
                                    .FontSize(7);
                            }
                        }

                        column.Item().PaddingTop(4).LineHorizontal(0.5f);

                        foreach (var cat in receipt.CategoryTotals)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{cat.CategoryName} Total").FontSize(8);
                                row.ConstantItem(72)
                                    .AlignRight()
                                    .Text(ColombianMoneyFormat.Format(cat.Total))
                                    .FontSize(8);
                            });
                        }

                        column.Item().PaddingTop(2).LineHorizontal(0.25f);

                        AddTotalRow(column, "Sub Total", receipt.Subtotal);

                        if (receipt.DiscountAmount > 0)
                        {
                            var discountLabel = receipt.DiscountPercent is > 0
                                ? $"Descuento ({receipt.DiscountPercent:N2}%)"
                                : "Descuento";
                            AddTotalRow(column, discountLabel, -receipt.DiscountAmount);
                        }

                        AddTotalRow(
                            column,
                            $"Impoconsumo ({receipt.ImpoconsumoPercent:N0}%)",
                            receipt.ImpoconsumoAmount);

                        if (receipt.TipAmount > 0)
                            AddTotalRow(column, "Propina", receipt.TipAmount);

                        column.Item().PaddingTop(2).LineHorizontal(0.5f);

                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Total").Bold().FontSize(10);
                            row.ConstantItem(72)
                                .AlignRight()
                                .Text(ColombianMoneyFormat.Format(receipt.Total))
                                .Bold()
                                .FontSize(10);
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    private static void AddTotalRow(ColumnDescriptor column, string label, decimal amount)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(8);
            row.ConstantItem(72).AlignRight().Text(ColombianMoneyFormat.Format(amount)).FontSize(8);
        });
    }
}
