using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Masking;

/// <summary>
/// Holds the PII masking rules (emails, phone numbers, sensitive property names) and exposes
/// them as a Serilog destructuring policy for non-string values.
/// </summary>
/// <remarks>
/// Serilog converts strings with its built-in scalar policy before any destructuring policy
/// runs, so this policy is never offered a string. The package pipeline masks through
/// <see cref="PiiMaskingEnricher"/>, which reuses these rules. Kept public for existing consumers.
/// </remarks>
public partial class PiiMaskingPolicy : IDestructuringPolicy
{
    // Matches: user@domain.com
    [GeneratedRegex(@"^([^@]{1})([^@]*)(@.+)$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    // Characters a written phone number may use: optional leading +, digits, spaces, - ( ).
    [GeneratedRegex(@"^\+?[\d\s\-\(\)]+$", RegexOptions.Compiled)]
    private static partial Regex PhoneCharactersRegex();

    // yyyy-MM-dd: has a separator and 8 digits, but is a date, not a phone number.
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled)]
    private static partial Regex IsoDateRegex();

    private const int MinPhoneDigits = 8;
    private const int MaxPhoneDigits = 15;
    private const int VisiblePhoneDigits = 4;
    private const string Redacted = "***REDACTED***";

    // A property whose name contains one of these is masked whatever its value looks like.
    private static readonly string[] PhoneNameMarkers = ["Phone", "Mobile", "Msisdn", "Tel"];

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

        if (value.Contains('@'))
        {
            var emailMatch = EmailRegex().Match(value);
            return emailMatch.Success ? MaskEmail(emailMatch) : value;
        }

        return LooksLikePhone(value) ? MaskPhone(value) : value;
    }

    /// <summary>
    /// Tight phone rule (OBS-1 owner decision Q2 "PII rule"): 8-15 digits written only with
    /// phone characters, and either a leading <c>+</c> or at least one separator. An ISO date,
    /// an all-digit order reference and a numeric correlation id do not qualify.
    /// </summary>
    internal static bool LooksLikePhone(string value)
    {
        var digits = value.Count(char.IsDigit);
        if (digits is < MinPhoneDigits or > MaxPhoneDigits) return false;
        if (!PhoneCharactersRegex().IsMatch(value)) return false;
        if (value[0] == '+') return true;

        var hasSeparator = digits != value.Length;
        return hasSeparator && !IsoDateRegex().IsMatch(value);
    }

    /// <summary>
    /// Whether the property name marks a phone number (contains Phone, Mobile, Msisdn or Tel,
    /// case-insensitive).
    /// </summary>
    internal static bool IsPhoneName(string propertyName) =>
        PhoneNameMarkers.Any(marker => propertyName.Contains(marker, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Masks the value of a phone-named property: last four digits when it reads as a phone
    /// number, fully redacted otherwise.
    /// </summary>
    internal static string MaskPhoneNamedValue(string? value) =>
        value is not null && LooksLikePhone(value) ? MaskPhone(value) : Redacted;

    /// <summary>
    /// Masks a property value if the property name is in the sensitive names list.
    /// </summary>
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
        if (digits.Length < VisiblePhoneDigits) return phone;

        // Show only the last 4 digits
        var lastFour = digits[^VisiblePhoneDigits..];
        var maskedPrefix = string.Join("-",
            Enumerable.Repeat("***", Math.Max(1, (digits.Length - VisiblePhoneDigits) / 3)));

        return $"{maskedPrefix}-{lastFour}";
    }
}
