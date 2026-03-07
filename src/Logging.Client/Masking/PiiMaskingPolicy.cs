using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Masking;

/// <summary>
/// A Serilog destructuring policy that masks PII (emails, phone numbers)
/// and sensitive property values in structured log output.
/// </summary>
public partial class PiiMaskingPolicy : IDestructuringPolicy
{
    // Matches: user@domain.com
    [GeneratedRegex(@"^([^@]{1})([^@]*)(@.+)$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    // Matches: +1-234-567-8901, (234) 567-8901, 234-567-8901, 2345678901, etc.
    [GeneratedRegex(@"^[\+]?[\d\s\-\(\)]{7,}$", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    private const int MinPhoneDigits = 4;

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

    /// <summary>
    /// Masks a string value if it matches known PII patterns (email or phone).
    /// Returns the original string if no PII is detected.
    /// </summary>
    internal static string MaskIfPii(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // Check email pattern
        var emailMatch = EmailRegex().Match(value);
        if (emailMatch.Success) return MaskEmail(emailMatch);

        // Check phone pattern
        if (PhoneRegex().IsMatch(value)) return MaskPhone(value);

        return value;
    }

    /// <summary>
    /// Masks a property value if the property name is in the sensitive names list.
    /// </summary>
    internal static string MaskSensitiveProperty(string propertyName, string value)
    {
        if (SensitivePropertyNames.Names.Contains(propertyName))
            return "***REDACTED***";

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

        // Get last char before @ if available
        var lastChar = middle.Length > 0
            ? middle[^1].ToString()
            : "";

        return $"{firstChar}{maskedMiddle}{lastChar}{domain}";
    }

    private static string MaskPhone(string phone)
    {
        // Extract just the digits
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < MinPhoneDigits) return phone;

        // Show only the last 4 digits
        var lastFour = digits[^4..];
        var maskedPrefix = string.Join("-",
            Enumerable.Repeat("***", Math.Max(1, (digits.Length - 4) / 3)));

        return $"{maskedPrefix}-{lastFour}";
    }
}
