using System.Globalization;

namespace Kohami.Commerce;

internal static class MockCardValidator
{
    public static bool IsValid(string number, string expiryDate, string securityCode, DateTimeOffset now)
    {
        if (number.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-'))) return false;
        var digits = new string(number.Where(char.IsAsciiDigit).ToArray());
        var brand = Brand(digits);
        if (brand is null || !HasValidChecksum(digits)) return false;

        var codeLength = brand == "amex" ? 4 : 3;
        if (securityCode.Length != codeLength || securityCode.Any(c => !char.IsAsciiDigit(c))) return false;
        if (!DateTime.TryParseExact(expiryDate, "MM/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry)) return false;

        var expiryMonth = new DateTime(expiry.Year, expiry.Month, 1);
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        return expiryMonth >= currentMonth;
    }

    private static string? Brand(string number)
    {
        if (number.StartsWith('4') && number.Length is 13 or 16 or 19) return "visa";
        if (number.Length == 16 && (int.TryParse(number[..2], out var prefix) && prefix is >= 51 and <= 55
            || int.TryParse(number[..4], out prefix) && prefix is >= 2221 and <= 2720)) return "mastercard";
        if (number.Length == 15 && (number.StartsWith("34", StringComparison.Ordinal) || number.StartsWith("37", StringComparison.Ordinal))) return "amex";
        if (number.Length is >= 16 and <= 19 && (number.StartsWith("6011", StringComparison.Ordinal) || number.StartsWith("65", StringComparison.Ordinal)
            || (int.TryParse(number[..3], out var discoverPrefix) && discoverPrefix is >= 644 and <= 649))) return "discover";
        if (number.Length is >= 16 and <= 19 && int.TryParse(number[..4], out var jcbPrefix) && jcbPrefix is >= 3528 and <= 3589) return "jcb";
        return null;
    }

    private static bool HasValidChecksum(string number)
    {
        var sum = 0;
        var doubleDigit = false;
        for (var index = number.Length - 1; index >= 0; index--)
        {
            var digit = number[index] - '0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
            doubleDigit = !doubleDigit;
        }
        return sum % 10 == 0;
    }
}
