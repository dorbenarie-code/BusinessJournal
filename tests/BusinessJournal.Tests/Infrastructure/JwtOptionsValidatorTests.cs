using BusinessJournal.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace BusinessJournal.Tests.Infrastructure;

public sealed class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenSigningKeyIsMissing_Fails(string? signingKey)
    {
        var result = _validator.Validate(Options.DefaultName, CreateOptions(signingKey!));

        Assert.True(result.Failed);
        Assert.Contains("Jwt:SigningKey is required", result.FailureMessage);
    }

    [Fact]
    public void Validate_WhenSigningKeyIsKnownPlaceholder_Fails()
    {
        Assert.NotEmpty(JwtOptionsValidator.KnownPlaceholderSigningKeys);

        foreach (var placeholder in JwtOptionsValidator.KnownPlaceholderSigningKeys)
        {
            var result = _validator.Validate(Options.DefaultName, CreateOptions(placeholder));

            Assert.True(result.Failed);
            Assert.Contains("published example value", result.FailureMessage);
            Assert.DoesNotContain(placeholder, result.FailureMessage);
        }
    }

    [Fact]
    public void Validate_WhenSigningKeyIsShorterThan32Bytes_Fails()
    {
        var shortKey = new string('k', JwtOptionsValidator.MinimumSigningKeyBytes - 1);

        var result = _validator.Validate(Options.DefaultName, CreateOptions(shortKey));

        Assert.True(result.Failed);
        Assert.Contains("at least 32 bytes", result.FailureMessage);
        Assert.DoesNotContain(shortKey, result.FailureMessage);
    }

    [Fact]
    public void Validate_WhenSigningKeyHasAtLeast32Bytes_Succeeds()
    {
        var key = new string('k', JwtOptionsValidator.MinimumSigningKeyBytes);

        var result = _validator.Validate(Options.DefaultName, CreateOptions(key));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void JwtTokenGenerator_WhenCreatedWithPlaceholderKey_Throws()
    {
        var placeholder = JwtOptionsValidator.KnownPlaceholderSigningKeys.First();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            new JwtTokenGenerator(Options.Create(CreateOptions(placeholder))));

        Assert.Contains("Jwt:SigningKey", exception.Message);
        Assert.DoesNotContain(placeholder, exception.Message);
    }

    private static JwtOptions CreateOptions(string signingKey)
    {
        return new JwtOptions
        {
            Issuer = "BusinessJournal.Tests",
            Audience = "BusinessJournal.Tests",
            SigningKey = signingKey,
            ExpirationMinutes = 60
        };
    }
}
