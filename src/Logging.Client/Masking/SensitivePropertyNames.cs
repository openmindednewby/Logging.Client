namespace Logging.Client.Masking;

/// <summary>Contains the set of property names considered sensitive for PII masking.</summary>
public static class SensitivePropertyNames
{
    /// <summary>Property names whose values should be completely masked in logs.</summary>
    public static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "Token",
        "Secret",
        "ApiKey",
        "Authorization",
        "CreditCard",
        "CardNumber",
        "Cvv",
        "Ssn",
        "SocialSecurityNumber",
        "AccessToken",
        "RefreshToken",
        "ClientSecret",
        "ConnectionString",
        "PrivateKey",
    };
}
