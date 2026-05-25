using System.Globalization;

namespace Restaurant.Infrastructure.SalesReceipts;

internal static class ColombianMoneyFormat
{
    private static readonly CultureInfo EsCo = CultureInfo.GetCultureInfo("es-CO");

    public static string Format(decimal amount) =>
        amount.ToString("C0", EsCo).Replace(" ", "");
}
