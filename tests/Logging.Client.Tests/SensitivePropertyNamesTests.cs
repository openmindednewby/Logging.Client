using Logging.Client.Masking;

namespace Logging.Client.Tests;

public class SensitivePropertyNamesTests
{
    [Fact]
    public void Names_ContainsAllExpectedProperties()
    {
        var expected = new[]
        {
            "Password", "Token", "Secret", "ApiKey", "Authorization",
            "CreditCard", "CardNumber", "Cvv", "Ssn", "SocialSecurityNumber",
            "AccessToken", "RefreshToken", "ClientSecret", "ConnectionString", "PrivateKey",
        };

        foreach (var name in expected)
        {
            SensitivePropertyNames.Names.Should().Contain(name);
        }
    }

    [Fact]
    public void Names_IsCaseInsensitive()
    {
        SensitivePropertyNames.Names.Contains("password").Should().BeTrue();
        SensitivePropertyNames.Names.Contains("PASSWORD").Should().BeTrue();
        SensitivePropertyNames.Names.Contains("Password").Should().BeTrue();
        SensitivePropertyNames.Names.Contains("pAsSwOrD").Should().BeTrue();
    }

    [Fact]
    public void Names_DoesNotContainNonSensitiveProperties()
    {
        SensitivePropertyNames.Names.Contains("UserName").Should().BeFalse();
        SensitivePropertyNames.Names.Contains("Email").Should().BeFalse();
        SensitivePropertyNames.Names.Contains("OrderId").Should().BeFalse();
    }
}
