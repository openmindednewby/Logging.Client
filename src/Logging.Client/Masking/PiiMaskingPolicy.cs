using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Masking;

/// <summary>Holds the PII masking rules and exposes them as a Serilog destructuring policy.</summary>
public partial class PiiMaskingPolicy : IDestructuringPolicy
{
    [GeneratedRegex(@"^([^@]{1})([^@]*)(@.+)$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\+?[\d\s\-\(\)]+$", RegexOptions.Compiled)]
    private static partial Regex PhoneCharactersRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled)]
    private static partial Regex IsoDateRegex();

    [GeneratedRegex(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+", RegexOptions.Compiled)]
    private static partial Regex NameTokenRegex();

    private const int MinPhoneDigits = 8;
    private const int MaxPhoneDigits = 15;
    private const int VisiblePhoneDigits = 4;
    private const string Redacted = "***REDACTED***";

    private static readonly HashSet<string> PhoneNameTokens =
        new(["Phone", "Mobile", "Msisdn", "Tel", "Telephone"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Attempts to destructure the given value, masking PII content.
    /// </summary>
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        result = null;

        if (value is not string stringValue) return false;

        var masked = MaskIfPii(stringValue);
        if (masked == stringValue) return false;

        result = new ScalarValue(masked);
        return true;
    }

    internal static string MaskIfPii(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        if (value.Contains('@'))
        {
            var emailMatch = EmailRegex().Match(value);
            return emailMatch.Success ? MaskEmail(emailMatch) : value;
        }

        return LooksLikePhone(value) ? MaskPhone(value) : value;
    }

    internal static bool LooksLikePhone(string value)
    {
        var digits = value.Count(char.IsDigit);
        if (digits is < MinPhoneDigits or > MaxPhoneDigits) return false;
        if (!PhoneCharactersRegex().IsMatch(value)) return false;
        if (value[0] == '+') return true;

        var hasSeparator = digits != value.Length;
        return hasSeparator && !IsoDateRegex().IsMatch(value);
    }

    internal static bool IsPhoneName(string propertyName)
    {
        foreach (Match token in NameTokenRegex().Matches(propertyName))
        {
            if (PhoneNameTokens.Contains(token.Value)) return true;
        }

        return false;
    }

    internal static string MaskPhoneNamedValue(string? value) =>
        value is not null && LooksLikePhone(value) ? MaskPhone(value) : Redacted;

    internal static string MaskSensitiveProperty(string propertyName, string value)
    {
        if (SensitivePropertyNames.Names.Contains(propertyName))
            return Redacted;

        if (IsPhoneName(propertyName)) return MaskPhoneNamedValue(value);

        return MaskIfPii(value);
    }

    private static string MaskEmail(Match match)
    {
        var firstChar = match.Groups[1].Value;
        var middle = match.Groups[2].Value;
        var domain = match.Groups[3].Value;

        var maskedMiddle = middle.Length > 0
            ? new string('*', Math.Min(middle.Length, 3))
            : "";

        var lastChar = middle.Length > 0
            ? middle[^1].ToString()
            : "";

        return $"{firstChar}{maskedMiddle}{lastChar}{domain}";
    }

    private static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < VisiblePhoneDigits) return phone;

        var lastFour = digits[^VisiblePhoneDigits..];
        var maskedPrefix = string.Join("-",
            Enumerable.Repeat("***", Math.Max(1, (digits.Length - VisiblePhoneDigits) / 3)));

        return $"{maskedPrefix}-{lastFour}";
    }
}
