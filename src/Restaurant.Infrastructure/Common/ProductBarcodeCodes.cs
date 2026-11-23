using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Restaurant.Infrastructure.Common;

/// <summary>Internal sticker codes (QR payload), aligned with the web generator.</summary>
public static class ProductBarcodeCodes
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string Generate(string? productName)
    {
        var slug = Slug(productName);
        var prefix = string.IsNullOrEmpty(slug) ? "PROD" : slug;
        var time = ToBase36((ulong)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_679_616)).PadLeft(4, '0');
        var rand = RandomSuffix(4);
        var code = $"{prefix}-{time}{rand}";
        return code.Length <= 32 ? code : code[..32];
    }

    private static string Slug(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return string.Empty;

        var normalized = productName.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;
            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
                sb.Append(char.ToUpperInvariant(ch));
        }

        var slug = sb.ToString();
        return slug.Length <= 8 ? slug : slug[..8];
    }

    private static string RandomSuffix(int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    private static string ToBase36(ulong value)
    {
        if (value == 0)
            return "0";
        Span<char> buffer = stackalloc char[16];
        var i = buffer.Length;
        while (value > 0)
        {
            buffer[--i] = Alphabet[(int)(value % 36)];
            value /= 36;
        }

        return new string(buffer[i..]);
    }
}
