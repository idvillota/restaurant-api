using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Restaurant.Application.Features.Sales.SalesReceipts;

namespace Restaurant.Infrastructure.SalesReceipts;

internal static class QuestPdfSalesReceiptDocument
{
    private const float TicketWidthMm = 80f;
    private static readonly CultureInfo EsCo = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] BuildPdf(SalesReceiptModel receipt)
    {
        var issuedLocal = receipt.IssuedAtUtc.ToLocalTime();
        var tenant = receipt.Tenant;
        var dateText = issuedLocal.ToString("MMM. dd yyyy hh:mm tt", EsCo).ToLowerInvariant();
        var rangeLabel = string.IsNullOrWhiteSpace(tenant.InvoiceNumberPrefix)
            ? "—"
            : tenant.InvoiceNumberPrefix.Trim();

        return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.ContinuousSize(TicketWidthMm, Unit.Millimetre);
                    page.MarginHorizontal(2.5f, Unit.Millimetre);
                    page.MarginVertical(3, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(7));

                    page.Content().Column(column =>
                    {
                        column.Spacing(2);

                        // —— Encabezado negocio ——
                        if (!string.IsNullOrWhiteSpace(tenant.TradeName))
                            column.Item().AlignCenter().Text(tenant.TradeName).Bold().FontSize(11);

                        if (!string.IsNullOrWhiteSpace(tenant.LegalName))
                            column.Item().AlignCenter().Text(tenant.LegalName).FontSize(7);

                        if (!string.IsNullOrWhiteSpace(tenant.TaxId))
                            column.Item().AlignCenter().Text(tenant.TaxId).FontSize(7);

                        if (!string.IsNullOrWhiteSpace(tenant.AddressLine))
                            column.Item().AlignCenter().Text(tenant.AddressLine).FontSize(7);

                        if (!string.IsNullOrWhiteSpace(tenant.Phone))
                            column.Item().AlignCenter().Text(tenant.Phone).FontSize(7);

                        if (!string.IsNullOrWhiteSpace(tenant.TaxRegime))
                            column.Item().AlignCenter().Text(tenant.TaxRegime).FontSize(6);

                        if (!string.IsNullOrWhiteSpace(tenant.LegalRepresentative))
                            column.Item().AlignCenter().Text(tenant.LegalRepresentative).FontSize(6);

                        column.Item().PaddingTop(3).LineHorizontal(0.5f);

                        // —— Factura electrónica ——
                        column.Item().AlignCenter().Text("FACTURA ELECTRÓNICA").Bold().FontSize(9);

                        column.Item().PaddingTop(2).Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text(receipt.InvoiceDisplayNumber).Bold().FontSize(8);
                                left.Item().Text("ORIGINAL").FontSize(7);
                                left.Item().Text(dateText).FontSize(7);
                                if (!string.IsNullOrWhiteSpace(receipt.TableCodes))
                                    left.Item().Text($"Sala-Mesa: {receipt.TableCodes}").FontSize(7);
                            });

                            row.RelativeItem().Column(right =>
                            {
                                right.Item().AlignRight().Text($"Identificador: {receipt.DianConsecutiveNumber}").FontSize(7);
                                if (!string.IsNullOrWhiteSpace(receipt.CashierName))
                                    right.Item().AlignRight().Text(receipt.CashierName).Bold().FontSize(7);
                            });
                        });

                        column.Item().PaddingTop(2).LineHorizontal(0.5f);

                        // —— Cliente ——
                        column.Item().Text(receipt.CustomerName.ToUpperInvariant()).Bold().FontSize(8);
                        if (!string.IsNullOrWhiteSpace(receipt.CustomerTaxId))
                        {
                            column.Item().Text($"CC : {receipt.CustomerTaxId}").FontSize(7);
                        }

                        column.Item().PaddingTop(2).LineHorizontal(0.5f);

                        // —— Detalle ítems (sin agrupar por categoría) ——
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2.8f);
                                cols.ConstantColumn(14);
                                cols.ConstantColumn(12);
                                cols.ConstantColumn(14);
                                cols.ConstantColumn(14);
                                cols.ConstantColumn(36);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("DESCRIPCIÓN").Bold().FontSize(6);
                                header.Cell().AlignCenter().Text("CAN").Bold().FontSize(6);
                                header.Cell().AlignCenter().Text("REF").Bold().FontSize(6);
                                header.Cell().AlignCenter().Text("UM").Bold().FontSize(6);
                                header.Cell().AlignCenter().Text("%IMP").Bold().FontSize(6);
                                header.Cell().AlignRight().Text("VALOR").Bold().FontSize(6);
                            });

                            foreach (var line in receipt.Lines)
                            {
                                table.Cell().Text(line.ProductName).FontSize(7);
                                table.Cell().AlignCenter().Text(FormatQuantity(line.Quantity)).FontSize(7);
                                table.Cell().AlignCenter().Text("").FontSize(7);
                                table.Cell().AlignCenter().Text("uds").FontSize(7);
                                table.Cell().AlignCenter().Text($"{receipt.ImpoconsumoPercent:N0}").FontSize(7);
                                table.Cell().AlignRight().Text(ColombianMoneyFormat.Format(line.LineTotal)).FontSize(7);

                                if (!string.IsNullOrWhiteSpace(line.Notes))
                                {
                                    table.Cell().ColumnSpan(6).PaddingLeft(4)
                                        .Text($"+ {line.Notes}").Italic().FontSize(6);
                                }
                            }
                        });

                        column.Item().PaddingTop(3).LineHorizontal(0.5f);

                        // —— Total y pagos ——
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"{receipt.ArticleCount} Artículos").FontSize(8);
                            row.ConstantItem(80).AlignRight().Row(totalRow =>
                            {
                                totalRow.AutoItem().Text("TOTAL").Bold().FontSize(9);
                                totalRow.AutoItem().PaddingLeft(4).Text(ColombianMoneyFormat.Format(receipt.Total)).Bold().FontSize(9);
                            });
                        });

                        foreach (var payment in receipt.Payments)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text(payment.MethodLabel).FontSize(7);
                                row.ConstantItem(80).AlignRight().Row(payRow =>
                                {
                                    payRow.AutoItem().Text("Entregado").FontSize(7);
                                    payRow.AutoItem().PaddingLeft(4).Text(ColombianMoneyFormat.Format(payment.Amount)).FontSize(7);
                                });
                            });
                        }

                        if (receipt.ChangeDue > 0)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Cambio").FontSize(7);
                                row.ConstantItem(80).AlignRight().Text(ColombianMoneyFormat.Format(receipt.ChangeDue)).FontSize(7);
                            });
                        }

                        if (receipt.DiscountAmount > 0)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Descuento").FontSize(7);
                                row.ConstantItem(80).AlignRight().Text($"−{ColombianMoneyFormat.Format(receipt.DiscountAmount)}").FontSize(7);
                            });
                        }

                        if (receipt.TipAmount > 0)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Propina").FontSize(7);
                                row.ConstantItem(80).AlignRight().Text(ColombianMoneyFormat.Format(receipt.TipAmount)).FontSize(7);
                            });
                        }

                        column.Item().PaddingTop(2).LineHorizontal(0.5f);

                        // —— Impuestos incluidos ——
                        column.Item().Table(taxTable =>
                        {
                            taxTable.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.ConstantColumn(42);
                                c.ConstantColumn(42);
                            });

                            taxTable.Header(h =>
                            {
                                h.Cell().Text("Impuestos incluidos").Bold().FontSize(6);
                                h.Cell().AlignRight().Text("Base").Bold().FontSize(6);
                                h.Cell().AlignRight().Text("Impuesto").Bold().FontSize(6);
                            });

                            taxTable.Cell().Text($"IMPOCONSUMO {receipt.ImpoconsumoPercent:N0}%").FontSize(7);
                            taxTable.Cell().AlignRight().Text(ColombianMoneyFormat.Format(receipt.ImpoconsumoBase)).FontSize(7);
                            taxTable.Cell().AlignRight().Text(ColombianMoneyFormat.Format(receipt.ImpoconsumoAmount)).FontSize(7);
                        });

                        column.Item().PaddingTop(4).LineHorizontal(0.5f);

                        // —— Pie DIAN ——
                        if (!string.IsNullOrWhiteSpace(tenant.DianResolutionNumber))
                        {
                            column.Item().AlignCenter().Text($"Resolución DIAN {tenant.DianResolutionNumber}").FontSize(6);
                        }

                        column.Item().AlignCenter()
                            .Text(
                                $"RANGO {rangeLabel} (Desde {tenant.DianResolutionFrom} Hasta {tenant.DianResolutionTo})")
                            .FontSize(6);

                        column.Item().AlignCenter().Text("Autorizado").Bold().FontSize(7);
                    });
                });
            })
            .GeneratePdf();
    }

    private static string FormatQuantity(decimal quantity) =>
        quantity % 1 == 0 ? ((int)quantity).ToString() : quantity.ToString("0.##", EsCo);
}
