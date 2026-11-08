namespace BusinessJournal.Tests.TestSupport;

/// <summary>
/// Rejection cases of <see cref="DisposableTestDatabase"/> that are decided before any
/// connection is opened. None of these tests contacts a SQL Server.
/// </summary>
public sealed class DisposableTestDatabaseGuardTests
{
    private const string Token = "guard-test-token";

    [Fact]
    public void Verify_WhenConnectionStringIsMissing_Rejects()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DisposableTestDatabase.Verify(null, Token));

        Assert.Contains(DisposableTestDatabase.ConnectionStringVariable, exception.Message);
    }

    [Fact]
    public void Verify_WhenMarkerTokenIsMissing_Rejects()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DisposableTestDatabase.Verify("Server=127.0.0.1,1;Database=Guard_DisposableIT", null));

        Assert.Contains(DisposableTestDatabase.MarkerTokenVariable, exception.Message);
    }

    [Fact]
    public void Verify_WhenServerIsNotLoopback_RejectsWithoutConnecting()
    {
        // 192.0.2.0/24 is reserved for documentation; the guard rejects before opening a connection.
        const string connectionString = "Server=192.0.2.10,1433;Database=Guard_DisposableIT;Connect Timeout=1";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DisposableTestDatabase.Verify(connectionString, Token));

        Assert.Contains("not a loopback address", exception.Message);
        Assert.DoesNotContain("192.0.2.10", exception.Message);
    }

    [Fact]
    public void Verify_WhenDatabaseNameIsNotDisposable_RejectsWithoutConnecting()
    {
        const string connectionString = "Server=127.0.0.1,1;Database=BusinessJournal;Connect Timeout=1";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DisposableTestDatabase.Verify(connectionString, Token));

        Assert.Contains(DisposableTestDatabase.RequiredDatabaseSuffix, exception.Message);
        Assert.DoesNotContain("Database=BusinessJournal", exception.Message);
    }
}
