using System.Collections.ObjectModel;
using System.Text;
using Microsoft.Extensions.Options;

namespace BusinessJournal.Infrastructure.Security;

/// <summary>
/// Rules for <see cref="JwtOptions"/>, shared by API startup and <see cref="JwtTokenGenerator"/>.
/// Failure messages never contain the signing key.
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    /// <summary>
    /// HS256 requires a key of at least 256 bits.
    /// </summary>
    public const int MinimumSigningKeyBytes = 32;

    /// <summary>
    /// Example keys that were committed to this repository. They are public,
    /// so they must never sign or validate real tokens.
    /// </summary>
    public static IReadOnlyCollection<string> KnownPlaceholderSigningKeys { get; } =
        new ReadOnlyCollection<string>(new[]
        {
            "THIS_IS_A_DEVELOPMENT_KEY_CHANGE_IT_TO_A_LONG_RANDOM_SECRET"
        });

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = GetFailures(options);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Throws when the options are unsafe. Covers instances created without the
    /// options pipeline, for example with <see cref="Options.Create{TOptions}(TOptions)"/>.
    /// </summary>
    public static void EnsureValid(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = GetFailures(options);

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(
                Options.DefaultName,
                typeof(JwtOptions),
                failures);
        }
    }

    private static List<string> GetFailures(JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (options.ExpirationMinutes <= 0)
        {
            failures.Add("Jwt:ExpirationMinutes must be greater than zero.");
        }

        var signingKeyFailure = GetSigningKeyFailure(options.SigningKey);
        if (signingKeyFailure is not null)
        {
            failures.Add(signingKeyFailure);
        }

        return failures;
    }

    private static string? GetSigningKeyFailure(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return "Jwt:SigningKey is required. Set it with 'dotnet user-secrets' "
                + "or the Jwt__SigningKey environment variable.";
        }

        if (KnownPlaceholderSigningKeys.Contains(signingKey.Trim(), StringComparer.Ordinal))
        {
            return "Jwt:SigningKey is a published example value and cannot be used. "
                + "Configure a private random key.";
        }

        if (Encoding.UTF8.GetByteCount(signingKey) < MinimumSigningKeyBytes)
        {
            return $"Jwt:SigningKey must be at least {MinimumSigningKeyBytes} bytes "
                + "(256 bits) when UTF-8 encoded.";
        }

        return null;
    }
}
