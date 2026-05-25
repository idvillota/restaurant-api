using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Restaurant.Application.Features.Sales.SalesReceipts;

namespace Restaurant.Infrastructure.SalesReceipts;

internal static class SalesReceiptXmlBuilder
{
    private static readonly XNamespace Ns = "urn:restaurant:sales-receipt:v1";

    public static byte[] BuildXml(SalesReceiptModel receipt)
    {
        var issuedLocal = receipt.IssuedAtUtc.ToLocalTime();
        var tenant = receipt.Tenant;
        var inv = CultureInfo.InvariantCulture;

        var emisor = new XElement(
            Ns + "Emisor",
            Content(
                new XElement(Ns + "NombreComercial", tenant.TradeName),
                new XElement(Ns + "RazonSocial", tenant.LegalName),
                new XElement(Ns + "Regimen", tenant.TaxRegime),
                new XElement(Ns + "NIT", tenant.TaxId),
                El(Ns + "RepresentanteLegal", tenant.LegalRepresentative),
                new XElement(Ns + "Direccion", tenant.AddressLine),
                new XElement(Ns + "Ciudad", tenant.City),
                new XElement(Ns + "Pais", tenant.Country),
                El(Ns + "CodigoPostal", tenant.PostalCode),
                El(Ns + "Telefono", tenant.Phone)));

        var resolucion = new XElement(
            Ns + "ResolucionDIAN",
            Content(
                El(Ns + "Numero", tenant.DianResolutionNumber),
                new XElement(Ns + "RangoDesde", tenant.DianResolutionFrom.ToString(inv)),
                new XElement(Ns + "RangoHasta", tenant.DianResolutionTo.ToString(inv))));

        var factura = new XElement(
            Ns + "Factura",
            Content(
                new XElement(Ns + "ConsecutivoDIAN", receipt.DianConsecutiveNumber.ToString(inv)),
                new XElement(Ns + "NumeroFactura", receipt.InvoiceDisplayNumber),
                new XElement(Ns + "NumeroCuenta", receipt.BillNumber),
                new XElement(Ns + "FechaHora", issuedLocal.ToString("o")),
                El(Ns + "Mesas", receipt.TableCodes),
                El(Ns + "Pedidos", receipt.OrderNumbers)));

        var cliente = new XElement(
            Ns + "Cliente",
            Content(
                new XElement(Ns + "Nombre", receipt.CustomerName),
                El(Ns + "Identificacion", receipt.CustomerTaxId)));

        var items = new XElement(
            Ns + "Items",
            receipt.Lines.Select(line =>
                new XElement(
                    Ns + "Item",
                    Content(
                        new XElement(Ns + "Descripcion", line.ProductName),
                        new XElement(Ns + "Categoria", line.ProductTypeName),
                        new XElement(Ns + "Cantidad", line.Quantity.ToString(inv)),
                        new XElement(Ns + "PrecioUnitario", line.UnitPrice.ToString(inv)),
                        new XElement(Ns + "TotalLinea", line.LineTotal.ToString(inv)),
                        new XElement(Ns + "Impoconsumo", line.ImpoconsumoAmount.ToString(inv)),
                        El(Ns + "Notas", line.Notes)))));

        var categorias = new XElement(
            Ns + "TotalesPorCategoria",
            receipt.CategoryTotals.Select(cat =>
                new XElement(
                    Ns + "Categoria",
                    new XAttribute("nombre", cat.CategoryName),
                    cat.Total.ToString(inv))));

        var totales = new XElement(
            Ns + "Totales",
            Content(
                new XElement(Ns + "Subtotal", receipt.Subtotal.ToString(inv)),
                new XElement(Ns + "Descuento", receipt.DiscountAmount.ToString(inv)),
                El(Ns + "DescuentoPorcentaje", receipt.DiscountPercent?.ToString(inv)),
                new XElement(Ns + "ImpoconsumoPorcentaje", receipt.ImpoconsumoPercent.ToString(inv)),
                new XElement(Ns + "Impoconsumo", receipt.ImpoconsumoAmount.ToString(inv)),
                new XElement(Ns + "Propina", receipt.TipAmount.ToString(inv)),
                new XElement(Ns + "Total", receipt.Total.ToString(inv)),
                new XElement(Ns + "Moneda", receipt.CurrencyCode)));

        var root = new XElement(
            Ns + "FacturaVenta",
            new XAttribute("version", "1.0"),
            Content(
                emisor,
                resolucion,
                factura,
                cliente,
                El(Ns + "Cajero", receipt.CashierName),
                items,
                categorias,
                totales));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return Encoding.UTF8.GetBytes(doc.ToString());
    }

    private static object[] Content(params object?[] items) =>
        items.Where(i => i is not null).Cast<object>().ToArray();

    private static XElement? El(XName name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new XElement(name, value);
}
