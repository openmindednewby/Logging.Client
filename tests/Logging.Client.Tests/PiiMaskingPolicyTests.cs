using Logging.Client.Masking;
using Serilog.Events;

namespace Logging.Client.Tests;

public class PiiMaskingPolicyTests
{
    private readonly PiiMaskingPolicy _policy = new();

    // ===== Email Masking =====

    [Fact]
    public void MaskIfPii_StandardEmail_MasksMiddleCharacters()
    {
        var result = PiiMaskingPolicy.MaskIfPii("test@domain.com");

        result.Should().NotBe("test@domain.com");
        result.Should().Contain("@domain.com");
        result.Should().StartWith("t");
    }

    [Fact]
    public void MaskIfPii_ShortEmail_MasksCorrectly()
    {
        var result = PiiMaskingPolicy.MaskIfPii("ab@example.com");

        result.Should().NotBe("ab@example.com");
        result.Should().Contain("@example.com");
    }

    [Fact]
    public void MaskIfPii_SingleCharLocalPart_PreservesDomain()
    {
        var result = PiiMaskingPolicy.MaskIfPii("a@test.com");

        result.Should().Contain("@test.com");
    }

    [Fact]
    public void MaskIfPii_LongEmail_MasksMiddle()
    {
        var result = PiiMaskingPolicy.MaskIfPii("longusername@company.org");

        result.Should().NotBe("longusername@company.org");
        result.Should().StartWith("l");
        result.Should().Contain("@company.org");
    }

    // ===== Phone Masking =====

    [Fact]
    public void MaskIfPii_UsPhoneWithDashes_ShowsLastFourDigitsOnly()
    {
        var result = PiiMaskingPolicy.MaskIfPii("234-567-8901");

        result.Should().EndWith("8901");
        result.Should().Contain("***");
        result.Should().NotContain("234");
    }

    [Fact]
    public void MaskIfPii_PhoneWithCountryCode_MasksAllButLastFour()
    {
        var result = PiiMaskingPolicy.MaskIfPii("+1-234-567-8901");

        result.Should().EndWith("8901");
        result.Should().Contain("***");
    }

    [Fact]
    public void MaskIfPii_PhoneWithParens_MasksCorrectly()
    {
        var result = PiiMaskingPolicy.MaskIfPii("(234) 567-8901");

        result.Should().EndWith("8901");
        result.Should().Contain("***");
    }

    [Fact]
    public void MaskIfPii_PlainDigitPhone_MasksCorrectly()
    {
        var result = PiiMaskingPolicy.MaskIfPii("2345678901");

        result.Should().EndWith("8901");
        result.Should().Contain("***");
    }

    // ===== Non-PII Preservation =====

    [Fact]
    public void MaskIfPii_RegularString_ReturnsUnchanged()
    {
        var result = PiiMaskingPolicy.MaskIfPii("Hello World");

        result.Should().Be("Hello World");
    }

    [Fact]
    public void MaskIfPii_EmptyString_ReturnsEmpty()
    {
        var result = PiiMaskingPolicy.MaskIfPii("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void MaskIfPii_Guid_ReturnsUnchanged()
    {
        var guid = Guid.NewGuid().ToString();
        var result = PiiMaskingPolicy.MaskIfPii(guid);

        result.Should().Be(guid);
    }

    [Fact]
    public void MaskIfPii_Url_ReturnsUnchanged()
    {
        var url = "https://api.example.com/v1/users";
        var result = PiiMaskingPolicy.MaskIfPii(url);

        result.Should().Be(url);
    }

    [Fact]
    public void MaskIfPii_IpAddress_ReturnsUnchanged()
    {
        var ip = "192.168.1.100";
        var result = PiiMaskingPolicy.MaskIfPii(ip);

        result.Should().Be(ip);
    }

    // ===== Sensitive Property Names =====

    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("Token")]
    [InlineData("Secret")]
    [InlineData("ApiKey")]
    [InlineData("Authorization")]
    [InlineData("CreditCard")]
    [InlineData("Cvv")]
    [InlineData("Ssn")]
    [InlineData("AccessToken")]
    [InlineData("RefreshToken")]
    [InlineData("ClientSecret")]
    [InlineData("ConnectionString")]
    [InlineData("PrivateKey")]
    public void MaskSensitiveProperty_SensitiveName_ReturnsRedacted(string propertyName)
    {
        var result = PiiMaskingPolicy.MaskSensitiveProperty(propertyName, "some-value");

        result.Should().Be("***REDACTED***");
    }

    [Fact]
    public void MaskSensitiveProperty_NonSensitiveName_ReturnsPiiMaskedValue()
    {
        var result = PiiMaskingPolicy.MaskSensitiveProperty("Email", "user@test.com");

        // Should mask the email, not redact based on property name
        result.Should().NotBe("user@test.com");
        result.Should().Contain("@test.com");
    }

    [Fact]
    public void MaskSensitiveProperty_NonSensitiveNonPii_ReturnsOriginal()
    {
        var result = PiiMaskingPolicy.MaskSensitiveProperty("OrderId", "ORD-12345");

        result.Should().Be("ORD-12345");
    }

    // ===== IDestructuringPolicy Interface =====

    [Fact]
    public void TryDestructure_NonStringValue_ReturnsFalse()
    {
        var success = _policy.TryDestructure(42, new MockPropertyValueFactory(), out var result);

        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryDestructure_EmailString_ReturnsTrueWithMaskedValue()
    {
        var success = _policy.TryDestructure("user@test.com", new MockPropertyValueFactory(), out var result);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result.Should().BeOfType<ScalarValue>();
        var scalar = (ScalarValue)result!;
        scalar.Value!.ToString().Should().Contain("@test.com");
        scalar.Value.ToString().Should().NotBe("user@test.com");
    }

    [Fact]
    public void TryDestructure_NonPiiString_ReturnsFalse()
    {
        var success = _policy.TryDestructure("hello world", new MockPropertyValueFactory(), out var result);

        success.Should().BeFalse();
        result.Should().BeNull();
    }

    // ===== Edge Cases =====

    [Fact]
    public void MaskIfPii_ShortPhoneNumber_ReturnsUnchanged()
    {
        // Less than 7 chars, should not match phone pattern
        var result = PiiMaskingPolicy.MaskIfPii("1234");

        result.Should().Be("1234");
    }

    [Fact]
    public void MaskIfPii_EmailWithPlusSign_Masks()
    {
        var result = PiiMaskingPolicy.MaskIfPii("user+tag@gmail.com");

        result.Should().NotBe("user+tag@gmail.com");
        result.Should().Contain("@gmail.com");
    }

    // ===== Stress: PII Masking Under Load =====

    [Fact]
    public void MaskIfPii_ConcurrentCalls_ThreadSafe()
    {
        const int iterations = 1000;
        var emails = Enumerable.Range(0, iterations)
            .Select(i => $"user{i}@test{i}.com")
            .ToList();

        var results = new string[iterations];

        Parallel.For(0, iterations, i =>
        {
            results[i] = PiiMaskingPolicy.MaskIfPii(emails[i]);
        });

        for (var i = 0; i < iterations; i++)
        {
            results[i].Should().NotBe(emails[i], $"email at index {i} should be masked");
            results[i].Should().Contain($"@test{i}.com", $"domain at index {i} should be preserved");
        }
    }

    [Fact]
    public void MaskSensitiveProperty_HighThroughput_AllMasked()
    {
        const int iterations = 1000;
        var results = new string[iterations];

        Parallel.For(0, iterations, i =>
        {
            results[i] = PiiMaskingPolicy.MaskSensitiveProperty("Password", $"secret-{i}");
        });

        results.Should().AllBe("***REDACTED***");
    }

    /// <summary>
    /// Minimal mock for ILogEventPropertyValueFactory.
    /// </summary>
    private sealed class MockPropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false)
        {
            return new ScalarValue(value);
        }
    }
}
