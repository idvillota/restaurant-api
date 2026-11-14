using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Restaurant.Application.Common;
using Restaurant.Application.Features.Cashier;

namespace Restaurant.Infrastructure.Planillas;

internal static class QuestPdfPlanillaDocument
{
    private static readonly CultureInfo EsCo = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] BuildPdf(PlanillaDocumentModel model)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(model.Title).Bold().FontSize(16);
                    if (!string.IsNullOrWhiteSpace(model.TenantName))
                        col.Item().Text(model.TenantName).FontSize(11);
                    col.Item().Text($"Día operativo: {model.BusinessDate:dd/MM/yyyy}").FontSize(10);
                    col.Item().PaddingBottom(6).LineHorizontal(0.5f);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text("Resumen").Bold().FontSize(11);
                    column.Item().Text($"Ventas: {Money(model.TotalSales)}");
                    if (model.SeparateVitrinaCafeSales)
                        column.Item().Text($"Vitrina Café: {Money(model.VitrinaCafeSales)}");
                    column.Item().Text($"Pagos cobrados: {Money(model.CollectedPayments)}");
                    column.Item().Text($"Egresos: {Money(model.TotalEgresos)}");
                    if (model.TotalAjustesEntrada != 0)
                        column.Item().Text($"Ajustes de entrada: {Money(model.TotalAjustesEntrada)}");
                    column.Item().Text($"Compras pagadas en caja: {Money(model.TotalPurchasesPaid)}");
                    column.Item().Text($"Sobrante / faltante: {OverShortText(model.OverShort)}");
                    if (!string.IsNullOrWhiteSpace(model.DayNotes))
                        column.Item().Text($"Notas del día: {model.DayNotes}");

                    column.Item().PaddingTop(4).Text("Ventas por tipo de producto").Bold().FontSize(11);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.4f);
                        });
                        HeaderCell(table, "Tipo");
                        HeaderCell(table, "Cantidad", true);
                        HeaderCell(table, "Total", true);
                        foreach (var row in model.Categories)
                        {
                            table.Cell().Element(Body).Text(row.ProductTypeName);
                            table.Cell().Element(Body).AlignRight().Text(Qty(row.Quantity));
                            table.Cell().Element(Body).AlignRight().Text(Money(row.LineTotal));
                        }

                        if (model.Categories.Count == 0)
                        {
                            table.Cell().ColumnSpan(3).Element(Body).Text("Sin ventas pagadas.");
                        }
                    });

                    column.Item().Text("Pagos por medio").Bold().FontSize(11);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.4f);
                        });
                        HeaderCell(table, "Medio");
                        HeaderCell(table, "Pagos", true);
                        HeaderCell(table, "Total", true);
                        foreach (var row in model.PaymentsByMethod)
                        {
                            table.Cell().Element(Body).Text(row.MethodLabel);
                            table.Cell().Element(Body).AlignRight().Text(row.Count.ToString(EsCo));
                            table.Cell().Element(Body).AlignRight().Text(Money(row.Total));
                        }

                        if (model.PaymentsByMethod.Count == 0)
                            table.Cell().ColumnSpan(3).Element(Body).Text("Sin pagos.");
                    });

                    column.Item().Text("Egresos por tipo").Bold().FontSize(11);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.4f);
                        });
                        HeaderCell(table, "Tipo");
                        HeaderCell(table, "Movimientos", true);
                        HeaderCell(table, "Total", true);
                        foreach (var row in model.EgresosByType)
                        {
                            table.Cell().Element(Body).Text(row.TypeLabel);
                            table.Cell().Element(Body).AlignRight().Text(row.Count.ToString(EsCo));
                            table.Cell().Element(Body).AlignRight().Text(Money(row.Total));
                        }

                        if (model.EgresosByType.Count == 0)
                            table.Cell().ColumnSpan(3).Element(Body).Text("Sin egresos.");
                    });

                    foreach (var shift in model.Shifts)
                    {
                        column.Item().PaddingTop(8).LineHorizontal(0.4f);
                        column.Item().Text($"Turno · {shift.CashierEmail}").Bold().FontSize(11);
                        column.Item().Text(
                            $"Apertura: {Local(shift.OpenedAtUtc)} · Cierre: {(shift.ClosedAtUtc is { } closed ? Local(closed) : "—")}");
                        column.Item().Text($"Ventas: {Money(shift.TotalSales)}");
                        if (model.SeparateVitrinaCafeSales)
                            column.Item().Text($"Vitrina Café: {Money(shift.VitrinaCafeSales)}");
                        column.Item().Text($"Egresos: {Money(shift.TotalEgresos)}");
                        if (shift.TotalAjustesEntrada != 0)
                            column.Item().Text($"Ajustes de entrada: {Money(shift.TotalAjustesEntrada)}");
                        column.Item().Text($"Compras pagadas en caja: {Money(shift.TotalPurchasesPaid)}");
                        column.Item().Text($"Fondo inicial: {Money(shift.OpeningFloat)}");
                        column.Item().Text($"Efectivo esperado: {NullableMoney(shift.ExpectedCash)}");
                        column.Item().Text($"Efectivo contado: {NullableMoney(shift.CountedCash)}");
                        column.Item().Text($"Sobrante / faltante: {OverShortText(shift.OverShort)}");
                        if (!string.IsNullOrWhiteSpace(shift.Notes))
                            column.Item().Text($"Notas: {shift.Notes}");

                        column.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Column(sig =>
                            {
                                sig.Item().Height(28).LineHorizontal(0.6f);
                                sig.Item().AlignCenter().Text("Firma cajero").FontSize(8);
                            });
                            row.ConstantItem(16);
                            row.RelativeItem().Column(sig =>
                            {
                                sig.Item().Height(28).LineHorizontal(0.6f);
                                sig.Item().AlignCenter().Text("Firma responsable").FontSize(8);
                            });
                        });
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void HeaderCell(TableDescriptor table, string text, bool right = false)
    {
        var cell = table.Cell().Element(Header);
        if (right)
            cell.AlignRight().Text(text).Bold();
        else
            cell.Text(text).Bold();
    }

    private static IContainer Header(IContainer container) =>
        container.BorderBottom(0.6f).PaddingVertical(3).PaddingHorizontal(2);

    private static IContainer Body(IContainer container) =>
        container.BorderBottom(0.2f).PaddingVertical(2).PaddingHorizontal(2);

    private static string Money(decimal value) =>
        value.ToString("C0", EsCo);

    private static string NullableMoney(decimal? value) =>
        value is null ? "—" : Money(value.Value);

    private static string OverShortText(decimal? value)
    {
        if (value is null)
            return "Sin arqueo";
        if (value > 0)
            return $"Sobrante {Money(value.Value)}";
        if (value < 0)
            return $"Faltante {Money(Math.Abs(value.Value))}";
        return Money(0);
    }

    private static string Qty(decimal value) =>
        value == decimal.Truncate(value)
            ? value.ToString("N0", EsCo)
            : value.ToString("N2", EsCo);

    private static string Local(DateTime utc) =>
        ColombiaTime.ToBogota(utc).ToString("dd/MM/yyyy HH:mm", EsCo);
}
