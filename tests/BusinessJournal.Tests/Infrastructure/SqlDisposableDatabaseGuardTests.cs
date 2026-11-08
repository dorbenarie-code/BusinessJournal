using BusinessJournal.Tests.TestSupport;

namespace BusinessJournal.Tests.Infrastructure;

/// <summary>
/// Guard checks that need a real server: identity is proven by the marker inside the
/// connected database, not by the connection string text. Read-only.
/// </summary>
[Collection("SqlServer integration tests")]
public sealed class SqlDisposableDatabaseGuardTests
{
    [Fact]
    public void Verify_WhenConnectedDatabaseHasDifferentMarkerToken_Rejects()
    {
        var verifiedConnectionString = DisposableTestDatabase.ConnectionString;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DisposableTestDatabase.Verify(verifiedConnectionString, "not-the-issued-marker-token"));

        Assert.Contains("marker does not match", exception.Message);
    }
}
