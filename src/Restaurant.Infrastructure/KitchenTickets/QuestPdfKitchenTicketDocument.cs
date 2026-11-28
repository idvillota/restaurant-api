using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Restaurant.Application.Common;
using Restaurant.Application.Features.Sales.KitchenTickets;
using Restaurant.Domain.Enums;

namespace Restaurant.Infrastructure.KitchenTickets;

internal static class QuestPdfKitchenTicketDocument
{
    public static byte[] BuildPdf(KitchenTicketModel ticket)
    {
        var width = ThermalPaperWidth.Normalize(ticket.PaperWidthMm);
        var widthMm = ThermalPaperWidth.ToMillimetres(width);
        var narrow = ThermalPaperWidth.IsNarrow(width);
        var marginH = narrow ? 3f : 4f;
        var titleSize = narrow ? 12f : 14f;
        var lineQtySize = narrow ? 10f : 11f;
        var lineNameSize = narrow ? 9f : 10f;
        var metaSize = narrow ? 7f : 8f;
        var defaultSize = narrow ? 8f : 9f;
        var sentLocal = ColombiaTime.ToBogota(ticket.SentAtUtc);

        return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.ContinuousSize(widthMm, Unit.Millimetre);
                    page.MarginHorizontal(marginH, Unit.Millimetre);
                    page.MarginVertical(5, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(defaultSize));

                    page.Content().Column(column =>
                    {
                        column.Spacing(narrow ? 3 : 4);

                        column.Item().AlignCenter().Text(
                            ticket.IsCounterPickup
                                ? "MOSTRADOR"
                                : ticket.IsDelivery ? "DOMICILIO" : $"MESA {ticket.TableCode}").Bold().FontSize(titleSize);
                        if (ticket.IsCounterPickup)
                        {
                            var call = string.IsNullOrWhiteSpace(ticket.CounterPagerNumber)
                                ? ticket.CounterPhone
                                : $"Localizador {ticket.CounterPagerNumber}";
                            var who = string.Join(
                                " · ",
                                new[] { ticket.CounterCustomerName, call }
                                    .Where(part => !string.IsNullOrWhiteSpace(part)));
                            if (who.Length > 0)
                                column.Item().AlignCenter().Text(who).FontSize(metaSize);
                        }
                        else if (ticket.IsDelivery)
                        {
                            var destination = KitchenTicketModel.FormatDeliveryDestination(
                                ticket.DeliveryAddress,
                                ticket.DeliveryPhone);
                            if (!string.IsNullOrWhiteSpace(destination))
                                column.Item().AlignCenter().Text(destination).FontSize(metaSize);
                        }

                        if (ticket.IsCancellation)
                        {
                            column.Item().AlignCenter().Text("*** ANULACIÓN ***").Bold().FontSize(narrow ? 11 : 12);
                        }

                        if (!string.IsNullOrWhiteSpace(ticket.PrinterStationName))
                        {
                            column.Item().Text($"Estación: {ticket.PrinterStationName}").Bold().FontSize(defaultSize);
                        }

                        column.Item().Text(
                            ticket.IsCancellation
                                ? $"Solicitado por: {ticket.SentBy}"
                                : $"Mesero/a: {ticket.SentBy}");
                        column.Item().Text($"Pedido: {ticket.OrderNumber}").FontSize(metaSize);
                        column.Item()
                            .Text(
                                ticket.IsCancellation
                                    ? $"Anulado: {sentLocal:dd/MM/yyyy HH:mm}"
                                    : $"Enviado: {sentLocal:dd/MM/yyyy HH:mm}")
                            .FontSize(metaSize);

                        if (ticket.IsCancellation && !string.IsNullOrWhiteSpace(ticket.CancelReason))
                        {
                            column.Item().Text($"Motivo: {ticket.CancelReason}").Bold().FontSize(defaultSize);
                        }

                        column.Item().PaddingTop(4).LineHorizontal(0.5f);

                        foreach (var line in ticket.Lines)
                        {
                            column.Item().PaddingTop(narrow ? 4 : 6).Text(text =>
                            {
                                text.Span(FormatQuantity(line.Quantity)).Bold().FontSize(lineQtySize);
                                text.Span("  ");
                                text.Span(line.ProductName).Bold().FontSize(lineNameSize);
                            });

                            if (line.ExcludedIngredientNames.Count > 0)
                            {
                                column.Item()
                                    .PaddingLeft(8)
                                    .Text($"Sin: {string.Join(", ", line.ExcludedIngredientNames)}")
                                    .Italic()
                                    .FontSize(defaultSize);
                            }

                            if (!string.IsNullOrWhiteSpace(line.Notes))
                            {
                                column.Item()
                                    .PaddingLeft(8)
                                    .Text($"Nota: {line.Notes}")
                                    .FontSize(defaultSize);
                            }

                            column.Item().PaddingTop(2).LineHorizontal(0.25f);
                        }
                    });
                });
            })
            .GeneratePdf();
    }

    private static string FormatQuantity(decimal quantity) =>
        quantity % 1 == 0 ? ((int)quantity).ToString() : quantity.ToString("0.##");
}
