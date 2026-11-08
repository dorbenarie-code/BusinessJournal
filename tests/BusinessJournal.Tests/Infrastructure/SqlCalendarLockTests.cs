using BusinessJournal.Application.Common.Exceptions;
using BusinessJournal.Infrastructure.Data.SqlServer;
using BusinessJournal.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace BusinessJournal.Tests.Infrastructure;

/// <summary>
/// How sp_getapplock results are classified. None of these tests contacts a SQL Server.
/// </summary>
public sealed class SqlCalendarLockTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ThrowIfNotGranted_WhenTheLockIsGranted_DoesNotThrow(int returnCode)
    {
        SqlCalendarLock.ThrowIfNotGranted(returnCode);
    }

    [Fact]
    public void ThrowIfNotGranted_WhenTheLockTimedOut_ReportsBusy_NotASlotConflict()
    {
        var exception = Assert.Throws<ServiceUnavailableException>(() =>
            SqlCalendarLock.ThrowIfNotGranted(-1));

        Assert.Equal(SqlCalendarLock.BusyMessage, exception.Message);
    }

    [Theory]
    [InlineData(-2)]   // canceled
    [InlineData(-3)]   // deadlock victim; SQL Server does not roll back for this code
    [InlineData(-999)] // parameter validation or other call error
    [InlineData(-4)]   // undocumented
    public void ThrowIfNotGranted_WhenTheLockFailedOtherwise_ReportsAnUnexpectedFailure(int returnCode)
    {
        // Exact type: neither ServiceUnavailableException (503) nor ConflictException (409).
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlCalendarLock.ThrowIfNotGranted(returnCode));

        Assert.Contains(returnCode.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(30_001)]
    public void Repository_RejectsALockTimeoutThatIsNotPositiveAndBounded(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SqlAppointmentRepository(CreateUnusedConnectionFactory(), TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public void Repository_RejectsAnInfiniteLockTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SqlAppointmentRepository(CreateUnusedConnectionFactory(), Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public void DefaultLockTimeout_IsFinite()
    {
        Assert.InRange(SqlCalendarLock.DefaultTimeout, TimeSpan.FromMilliseconds(1), SqlCalendarLock.MaxTimeout);
    }

    private static SqlServerConnectionFactory CreateUnusedConnectionFactory()
    {
        // The factory only stores the string; nothing connects.
        return new SqlServerConnectionFactory(
            Options.Create(new SqlServerOptions
            {
                ConnectionString = "Server=unused.invalid;Database=Unused"
            }));
    }
}
