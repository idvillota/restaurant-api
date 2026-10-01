using System.Globalization;
using System.Xml.Linq;
using Restaurant.Application.Features.Sales.KitchenTickets;

namespace Restaurant.Infrastructure.KitchenTickets;

/// <summary>
/// Kitchen ticket XML in the shape the tablet already prints.
/// </summary>
internal static class KitchenTicketPrintXml
{
    public static string Write(KitchenTicketModel model)
    {
        var culture = CultureInfo.InvariantCulture;
        var ticket = new XElement(
            "KitchenTicket",
            Text("TableCode", model.TableCode),
            Text("OrderNumber", model.OrderNumber),
            Text("SentBy", model.SentBy),
            Text("SentAtUtc", model.SentAtUtc.ToString("o", culture)),
            new XElement(
                "PrinterStation",
                Text("Name", model.PrinterStationName ?? string.Empty),
                Text("Code", model.PrinterStationCode ?? string.Empty)),
            new XElement(
                "Lines",
                model.Lines.Select(line => new XElement(
                    "Line",
                    Text("ProductName", line.ProductName),
                    Text("Quantity", line.Quantity.ToString(culture)),
                    Text("Notes", line.Notes ?? string.Empty),
                    new XElement(
                        "ExcludedIngredients",
                        line.ExcludedIngredientNames.Select(name => Text("Ingredient", name)))))),
            Text("IsCancellation", model.IsCancellation ? "true" : "false"),
            Text("CancelReason", model.CancelReason ?? string.Empty));

        return new XDocument(ticket).ToString();
    }

    private static XElement Text(string name, string value) => new(name, value);
}
