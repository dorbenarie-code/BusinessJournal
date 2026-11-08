using BusinessJournal.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace BusinessJournal.Tests.ApiHost;

[Collection(ApiHostCollection.Name)]
public sealed class JwtStartupValidationTests
{
    // Only a case label is passed in, so key values never appear in test names or logs.
    [Theory]
    [InlineData("missing")]
    [InlineData("known-placeholder")]
    [InlineData("too-short")]
    public void ApiHost_WithUnsafeSigningKey_RefusesToStart(string unsafeKeyCase)
    {
        var signingKey = unsafeKeyCase switch
        {
            "missing" => string.Empty,
            "known-placeholder" => JwtOptionsValidator.KnownPlaceholderSigningKeys.First(),
            "too-short" => new string('k', JwtOptionsValidator.MinimumSigningKeyBytes - 1),
            _ => throw new ArgumentOutOfRangeException(nameof(unsafeKeyCase))
        };

        using var factory = new SqlFreeApiFactory(signingKey);

        var startupError = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(startupError);

        var validationError = FindException<OptionsValidationException>(startupError!);
        Assert.NotNull(validationError);
        Assert.Equal(typeof(JwtOptions), validationError!.OptionsType);
        Assert.Contains("Jwt:SigningKey", validationError.Message);

        if (signingKey.Length > 0)
        {
            Assert.DoesNotContain(signingKey, startupError!.ToString());
        }
    }

    private static T? FindException<T>(Exception exception)
        where T : Exception
    {
        if (exception is T match)
        {
            return match;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                var found = FindException<T>(inner);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        return exception.InnerException is null
            ? null
            : FindException<T>(exception.InnerException);
    }
}
