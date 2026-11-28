namespace Restaurant.Infrastructure.Services;

internal static class CounterPhoneNumbers
{
    /// <summary>
    /// Digits only, with a Colombian mobile (10 digits starting with 3) prefixed by 57.
    /// Returns null when the number cannot be given to WhatsApp.
    /// </summary>
    public static string? ToWhatsAppRecipient(string? raw)
    {
        var digits = Digits(raw);
        if (digits.Length == 10 && digits[0] == '3')
            digits = "57" + digits;

        return digits.Length is >= 11 and <= 15 ? digits : null;
    }

    /// <summary>Same key for 3001234567 and 573001234567.</summary>
    public static string MatchKey(string? raw)
    {
        var digits = Digits(raw);
        if (digits.Length == 12 && digits.StartsWith("57", StringComparison.Ordinal) && digits[2] == '3')
            return digits[2..];
        return digits;
    }

    public static string Digits(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        Span<char> buffer = stackalloc char[raw.Length];
        var count = 0;
        foreach (var ch in raw)
        {
            if (ch is >= '0' and <= '9')
                buffer[count++] = ch;
        }

        return new string(buffer[..count]);
    }
}
