using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Restaurant.Application.Common;
using Restaurant.Application.Features.Sales.SalesReceipts;
using Restaurant.Domain.Enums;

namespace Restaurant.Infrastructure.SalesReceipts;

internal static class QuestPdfSalesReceiptDocument
{
    private static readonly CultureInfo EsCo = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] BuildPdf(SalesReceiptModel receipt)
    {
        var width = ThermalPaperWidth.Normalize(receipt.PaperWidthMm);
        var widthMm = ThermalPaperWidth.ToMillimetres(width);
        var narrow = ThermalPaperWidth.IsNarrow(width);
        // Keep margins small; content must fit page width for both 58mm (~60) and 80mm.
        var marginH = narrow ? 1.5f : 2.5f;
        var contentMm = widthMm - (marginH * 2f);
        // QuestPDF absolute sizes are points (72pt = 1in). Convert so money column fits content.
        var moneyColPt = MmToPoints(narrow ? contentMm * 0.42f : contentMm * 0.38f);
        var issuedLocal = ColombiaTime.ToBogota(receipt.IssuedAtUtc);
        var tenant = receipt.Tenant;
        var dateText = issuedLocal.ToString("MMM. dd yyyy hh:mm tt", EsCo).ToLowerInvariant();
        var rangeLabel = string.IsNullOrWhiteSpace(tenant.InvoiceNumberPrefix)
            ? "—"
            : tenant.InvoiceNumberPrefix.Trim();
        var showLegalName = ShouldShowLegalName(tenant.TradeName, tenant.LegalName);

        return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.ContinuousSize(widthMm, Unit.Millimetre);
                    page.MarginHorizontal(marginH, Unit.Millimetre);
                    page.MarginVertical(3, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(narrow ? 6.5f : 7));

                    page.Content().Column(column =>
                    {
                        column.Spacing(2);

                        // —— Encabezado negocio ——
                        if (receipt.LogoImageBytes is { Length: > 0 } logoBytes)
                        {
                            var logoMaxHeightMm = narrow ? 16f : 22f;
                            var logoMaxWidthMm = contentMm * 0.85f;
                            // Full-width row with side spacers so the logo box is truly centered.
                            column.Item().Row(row =>
                            {
                                row.RelativeItem();
                                row.ConstantItem(MmToPoints(logoMaxWidthMm))
                                    .Height(logoMaxHeightMm, Unit.Millimetre)
                                    .AlignCenter()
                                    .Image(logoBytes)
                                    .FitArea();
                                row.RelativeItem();
                            });
                        }

                        if (!string.IsNullOrWhiteSpace(tenant.TradeName))
                            column.Item().AlignCenter().Text(tenant.TradeName).Bold().FontSize(narrow ? 9 : 11);

                        if (showLegalName)
                            column.Item().AlignCenter().Text(tenant.LegalName).FontSize(narrow ? 6 : 7);

                        if (!string.IsNullOrWhiteSpace(tenant.TaxId))
                            column.Item().AlignCenter().Text(tenant.TaxId).FontSize(narrow ? 6 : 7);

                        if (!string.IsNullOrWhiteSpace(tenant.AddressLine))
                            column.Item().AlignCenter().Text(tenant.AddressLine).FontSize(narrow ? 6 : 7);

                        if (!string.IsNullOrWhiteSpace(tenant.Phone))
                            column.Item().AlignCenter().Text(tenant.Phone).FontSize(narrow ? 6 : 7);

                        if (!string.IsNullOrWhiteSpace(tenant.TaxRegime))
                            column.Item().AlignCenter().Text(tenant.TaxRegime).FontSize(6);

                        if (!string.IsNullOrWhiteSpace(tenant.LegalRepresentative))
                            column.Item().AlignCenter().Text(tenant.LegalRepresentative).FontSize(6);

                        column.Item().PaddingTop(3).LineHorizontal(0.5f);

                        // —— Factura POS ——
                        column.Item().AlignCenter().Text("FACTURA POS").Bold().FontSize(narrow ? 8 : 9);

                        column.Item().PaddingTop(2).Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Text(receipt.InvoiceDisplayNumber).Bold().FontSize(narrow ? 7 : 8);
                                left.Item().Text(dateText).FontSize(narrow ? 6 : 7);
                                if (!string.IsNullOrWhiteSpace(receipt.TableCodes))
                                    left.Item().Text($"Sala-Mesa: {receipt.TableCodes}").FontSize(narrow ? 6 : 7);
                            });

                            row.RelativeItem().Column(right =>
                            {
                                if (!string.IsNullOrWhiteSpace(receipt.CashierName))
                                {
                                    right.Item().AlignRight()
                                        .Text($"Cajero: {receipt.CashierName}")
                                        .FontSize(narrow ? 6 : 7);
                                }
                            });
                        });

                        column.Item().PaddingTop(2).LineHorizontal(0.5f);

                        // —— Cliente ——
                        column.Item().Text(receipt.CustomerName.ToUpperInvariant()).Bold().FontSize(narrow ? 7 : 8);
                        if (!string.IsNullOrWhiteSpace(receipt.CustomerTaxId))
                            column.Item().Text($"CC : {receipt.CustomerTaxId}").FontSize(narrow ? 6 : 7);

                        column.Item().PaddingTop(2).LineHorizontal(0.5f);

                        // —— Detalle ítems: DESCRIPCIÓN / CAN / VALOR (same for 58 and 80 mm) ——
                        column.Item().Table(table =>
                        {
                            var nameSize = narrow ? 6f : 7f;
                            var headerSize = narrow ? 5.5f : 6f;
                            var noteSize = narrow ? 5.5f : 6f;

                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3.6f);
                                cols.RelativeColumn(0.9f);
                                cols.RelativeColumn(1.8f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("DESCRIPCIÓN").Bold().FontSize(headerSize);
                                header.Cell().AlignCenter().Text("CAN").Bold().FontSize(headerSize);
                                header.Cell().AlignRight().Text("VALOR").Bold().FontSize(headerSize);
                            });

                            foreach (var line in receipt.Lines)
                            {
                                table.Cell().Text(line.ProductName).FontSize(nameSize);
                                table.Cell().AlignCenter().Text(FormatQuantity(line.Quantity)).FontSize(nameSize);
                                table.Cell().AlignRight().Text(ColombianMoneyFormat.Format(line.LineTotal)).FontSize(nameSize);

                                if (!string.IsNullOrWhiteSpace(line.Notes))
                                {
                                    table.Cell().ColumnSpan(3).PaddingLeft(narrow ? 2 : 4)
                                        .Text($"+ {line.Notes}").Italic().FontSize(noteSize);
                                }
                            }
                        });

                        column.Item().PaddingTop(3).LineHorizontal(0.5f);

                        // —— Total y pagos ——
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"{receipt.ArticleCount} Artículos").FontSize(narrow ? 7 : 8);
                            row.ConstantItem(moneyColPt).AlignRight().Text(
                                $"TOTAL {ColombianMoneyFormat.Format(receipt.Total)}").Bold().FontSize(narrow ? 7 : 9);
                        });

                        foreach (var payment in receipt.Payments)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text(payment.MethodLabel).FontSize(7);
                                row.ConstantItem(moneyColPt).AlignRight()
                                    .Text($"Entregado {ColombianMoneyFormat.Format(payment.Amount)}").FontSize(7);
                            });
                        }

                        if (receipt.ChangeDue > 0)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Cambio").FontSize(7);
                                row.ConstantItem(moneyColPt).AlignRight()
                                    .Text(ColombianMoneyFormat.Format(receipt.ChangeDue)).FontSize(7);
                            });
                        }

                        if (receipt.DiscountAmount > 0)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Descuento").FontSize(7);
                                row.ConstantItem(moneyColPt).AlignRight()
                                    .Text($"−{ColombianMoneyFormat.Format(receipt.DiscountAmount)}").FontSize(7);
                            });
                        }

                        if (receipt.TipAmount > 0)
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Propina").FontSize(7);
                                row.ConstantItem(moneyColPt).AlignRight()
                                    .Text(ColombianMoneyFormat.Format(receipt.TipAmount)).FontSize(7);
                            });
                        }

                        column.Item().PaddingTop(2).LineHorizontal(0.5f);

                        // —— Impuestos incluidos ——
                        column.Item().Table(taxTable =>
                        {
                            taxTable.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2f);
                                c.RelativeColumn(1f);
                                c.RelativeColumn(1f);
                            });

                            taxTable.Header(h =>
                            {
                                h.Cell().Text("Impuestos incluidos").Bold().FontSize(6);
                                h.Cell().AlignRight().Text("Base").Bold().FontSize(6);
                                h.Cell().AlignRight().Text("Impuesto").Bold().FontSize(6);
                            });

                            taxTable.Cell().Text($"IMPOCONSUMO {receipt.ImpoconsumoPercent:N0}%").FontSize(narrow ? 6 : 7);
                            taxTable.Cell().AlignRight().Text(ColombianMoneyFormat.Format(receipt.ImpoconsumoBase)).FontSize(narrow ? 6 : 7);
                            taxTable.Cell().AlignRight().Text(ColombianMoneyFormat.Format(receipt.ImpoconsumoAmount)).FontSize(narrow ? 6 : 7);
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

    private static bool ShouldShowLegalName(string? tradeName, string? legalName)
    {
        if (string.IsNullOrWhiteSpace(legalName))
            return false;

        if (string.IsNullOrWhiteSpace(tradeName))
            return true;

        return !string.Equals(tradeName.Trim(), legalName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static float MmToPoints(float mm) => mm * 72f / 25.4f;

    private static string FormatQuantity(decimal quantity) =>
        quantity % 1 == 0 ? ((int)quantity).ToString() : quantity.ToString("0.##", EsCo);
}
