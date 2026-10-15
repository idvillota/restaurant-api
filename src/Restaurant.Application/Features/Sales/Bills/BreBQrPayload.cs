using System.Globalization;
using System.Text;
using Restaurant.Domain.Enums;

namespace Restaurant.Application.Features.Sales.Bills;

/// <summary>
/// Builds a merchant-presented EMV QR for a Bre-B key and a fixed amount.
/// The amount already includes taxes and tip. Tax tags are zero so the payer's app does not add them again.
/// </summary>
public static class BreBQrPayload
{
    public const int MerchantNameMaxLength = 25;
    public const int CityMaxLength = 15;
    public const int PostalCodeMaxLength = 10;
    public const int KeyMaxLength = 40;
    public const int ReferenceMaxLength = 18;

    public static string Build(BreBQrInput input)
    {
        var key = RequireKey(input.Key);
        if (!Enum.IsDefined(input.KeyType))
            throw new InvalidOperationException("El tipo de llave Bre-B no es válido.");

        var amount = decimal.Round(input.Amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0)
            throw new InvalidOperationException("El monto del QR debe ser mayor que cero.");

        var amountText = amount.ToString("0.00", CultureInfo.InvariantCulture);
        if (amountText.Length > 13)
            throw new InvalidOperationException("El monto del QR supera el máximo permitido.");

        var merchant = MerchantText(input.MerchantName, MerchantNameMaxLength, "nombre comercial");
        var city = MerchantText(input.City, CityMaxLength, "ciudad");
        var postal = OptionalCode(input.PostalCode, PostalCodeMaxLength);
        var reference = RequireReference(input.Reference);
        var keyTag = ((int)input.KeyType).ToString("00", CultureInfo.InvariantCulture);

        var body = new StringBuilder();
        Append(body, "00", "01");
        Append(body, "01", "11");
        AppendTemplate(body, "26", ("00", "CO.COM.ACH.LLA"), (keyTag, key));
        AppendTemplate(body, "49", ("00", "CO.COM.ACH.RED"), ("01", "ACH"));
        AppendTemplate(body, "50", ("00", "CO.COM.ACH.CU"), ("01", key));
        Append(body, "52", "0000");
        Append(body, "53", "170");
        Append(body, "54", amountText);
        Append(body, "58", "CO");
        Append(body, "59", merchant);
        Append(body, "60", city);
        if (postal is not null)
            Append(body, "61", postal);
        AppendTemplate(body, "62", ("07", "0000"), ("08", "00"));
        AppendTemplate(body, "80", ("00", "CO.COM.ACH.CANAL"), ("01", "POS"));
        AppendTemplate(body, "81", ("00", "CO.COM.ACH.CIVA"), ("01", "02"));
        AppendTemplate(body, "82", ("00", "CO.COM.ACH.IVA"), ("01", "0"));
        AppendTemplate(body, "83", ("00", "CO.COM.ACH.BASE"), ("01", "0"));
        AppendTemplate(body, "84", ("00", "CO.COM.ACH.CINC"), ("01", "02"));
        AppendTemplate(body, "85", ("00", "CO.COM.ACH.INC"), ("01", "0"));
        AppendTemplate(body, "90", ("00", "CO.COM.ACH.TRXID"), ("01", reference));

        var withCrcTag = body + "6304";
        return withCrcTag + Crc16Ccitt(withCrcTag);
    }

    internal static string Crc16Ccitt(string value)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(value))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ 0x1021);
                else
                    crc <<= 1;
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }

    private static void Append(StringBuilder body, string tag, string value)
    {
        body.Append(tag);
        body.Append(value.Length.ToString("00", CultureInfo.InvariantCulture));
        body.Append(value);
    }

    private static void AppendTemplate(StringBuilder body, string tag, params (string Tag, string Value)[] fields)
    {
        var inner = new StringBuilder();
        foreach (var field in fields)
            Append(inner, field.Tag, field.Value);
        Append(body, tag, inner.ToString());
    }

    private static string RequireKey(string? key)
    {
        var value = key?.Trim() ?? string.Empty;
        if (value.Length is 0 or > KeyMaxLength || value.Any(c => c is < (char)33 or > (char)126))
            throw new InvalidOperationException("La llave Bre-B debe tener entre 1 y 40 caracteres simples, sin espacios.");
        return value;
    }

    private static string RequireReference(string? reference)
    {
        var value = reference?.Trim() ?? string.Empty;
        if (value.Length is 0 or > ReferenceMaxLength || value.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new InvalidOperationException("La referencia del QR no es válida.");
        return value;
    }

    private static string? OptionalCode(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var chars = value.Where(char.IsAsciiLetterOrDigit).ToArray();
        if (chars.Length == 0)
            return null;

        var text = new string(chars);
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string MerchantText(string? value, int maxLength, string label)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var chars = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsAsciiLetterOrDigit(c))
            {
                chars.Append(c);
                continue;
            }

            if (chars.Length > 0 && chars[^1] != ' ')
                chars.Append(' ');
        }

        var text = string.Join(' ', chars.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (text.Length == 0)
            throw new InvalidOperationException($"El {label} del local no puede usarse en el QR. Usa letras o números.");

        if (text.Length > maxLength)
            text = text[..maxLength].TrimEnd();

        return text;
    }
}

public sealed record BreBQrInput(
    string Key,
    BreBKeyType KeyType,
    decimal Amount,
    string MerchantName,
    string City,
    string? PostalCode,
    string Reference);
