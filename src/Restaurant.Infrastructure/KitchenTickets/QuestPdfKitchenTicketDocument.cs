using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Restaurant.Application.Features.Sales.KitchenTickets;

namespace Restaurant.Infrastructure.KitchenTickets;

internal static class QuestPdfKitchenTicketDocument
{
    private const float TicketWidthMm = 80f;

    public static byte[] BuildPdf(KitchenTicketModel ticket)
    {
        var sentLocal = ticket.SentAtUtc.ToLocalTime();

        return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.ContinuousSize(TicketWidthMm, Unit.Millimetre);
                    page.MarginHorizontal(4, Unit.Millimetre);
                    page.MarginVertical(5, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Content().Column(column =>
                    {
                        column.Spacing(4);

                        column.Item().AlignCenter().Text($"MESA {ticket.TableCode}").Bold().FontSize(14);

                        if (!string.IsNullOrWhiteSpace(ticket.PrinterStationName))
                        {
                            column.Item().Text($"Estación: {ticket.PrinterStationName}").Bold().FontSize(9);
                        }

                        column.Item().Text($"Mesero/a: {ticket.SentBy}");
                        column.Item().Text($"Pedido: {ticket.OrderNumber}").FontSize(8);
                        column.Item()
                            .Text($"Enviado: {sentLocal:dd/MM/yyyy HH:mm}")
                            .FontSize(8);

                        column.Item().PaddingTop(4).LineHorizontal(0.5f);

                        foreach (var line in ticket.Lines)
                        {
                            column.Item().PaddingTop(6).Text(text =>
                            {
                                text.Span(FormatQuantity(line.Quantity)).Bold().FontSize(11);
                                text.Span("  ");
                                text.Span(line.ProductName).Bold().FontSize(10);
                            });

                            if (line.ExcludedIngredientNames.Count > 0)
                            {
                                column.Item()
                                    .PaddingLeft(8)
                                    .Text($"Sin: {string.Join(", ", line.ExcludedIngredientNames)}")
                                    .Italic()
                                    .FontSize(9);
                            }

                            if (!string.IsNullOrWhiteSpace(line.Notes))
                            {
                                column.Item()
                                    .PaddingLeft(8)
                                    .Text($"Nota: {line.Notes}")
                                    .FontSize(9);
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
