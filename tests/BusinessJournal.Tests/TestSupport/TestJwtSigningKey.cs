using System.Security.Cryptography;

namespace BusinessJournal.Tests.TestSupport;

/// <summary>
/// JWT signing key for in-process API test hosts only.
/// </summary>
/// <remarks>
/// Generated in memory once per test run and never written anywhere, so no usable key
/// is committed. One value is shared by all test hosts in a run because the API test
/// helper caches a single access token across hosts.
/// </remarks>
internal static class TestJwtSigningKey
{
    public static string Value { get; } =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
