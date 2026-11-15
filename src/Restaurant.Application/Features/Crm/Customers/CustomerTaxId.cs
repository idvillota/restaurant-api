using System.Text;

namespace Restaurant.Application.Features.Crm.Customers;

public static class CustomerTaxId
{
    public const string ConsumidorFinal = "2222222";

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var digits = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (char.IsDigit(ch))
                digits.Append(ch);
        }

        return digits.Length == 0 ? null : digits.ToString();
    }

    public static bool IsConsumidorFinal(string? normalized) =>
        string.Equals(normalized, ConsumidorFinal, StringComparison.Ordinal);
}
