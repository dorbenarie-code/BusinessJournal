using System.Data;
using BusinessJournal.Application.Common.Exceptions;
using Microsoft.Data.SqlClient;

namespace BusinessJournal.Infrastructure.Data.SqlServer;

/// <summary>
/// Exclusive SQL Server application lock (<c>sp_getapplock</c>) that serializes every change
/// to the appointment calendar.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The calendar has a single provider, so one resource covers the whole calendar.</item>
/// <item>Every caller uses the same resource name, principal, mode and owner. The database is
/// the one the connection is bound to.</item>
/// <item>The lock is owned by the caller's transaction and is released only when that transaction
/// commits or rolls back, so the next holder sees the previous holder's writes as committed data.</item>
/// <item>The wait is always finite.</item>
/// </list>
/// </remarks>
public static class SqlCalendarLock
{
    /// <summary>Resource name. sp_getapplock compares it binary, so the casing matters.</summary>
    public const string ResourceName = "BusinessJournal.Calendar";

    public const string DbPrincipal = "public";

    /// <summary>Shown to API clients when the lock is not granted in time.</summary>
    public const string BusyMessage = "The calendar is busy. Please try again.";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(30);

    // The client must not give up before the server reports its own lock timeout.
    private const int CommandTimeoutMarginSeconds = 15;

    /// <summary>
    /// Acquires the calendar lock for <paramref name="transaction"/>, waiting at most
    /// <paramref name="timeout"/>. Throws if the lock is not granted; the caller must then roll back.
    /// </summary>
    public static void Acquire(SqlConnection connection, SqlTransaction transaction, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        EnsureValidTimeout(timeout);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "sys.sp_getapplock";
        command.CommandTimeout = (int)Math.Ceiling(timeout.TotalSeconds) + CommandTimeoutMarginSeconds;

        command.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255) { Value = ResourceName });
        command.Parameters.Add(new SqlParameter("@LockMode", SqlDbType.VarChar, 32) { Value = "Exclusive" });
        command.Parameters.Add(new SqlParameter("@LockOwner", SqlDbType.VarChar, 32) { Value = "Transaction" });
        command.Parameters.Add(new SqlParameter("@LockTimeout", SqlDbType.Int) { Value = (int)timeout.TotalMilliseconds });
        command.Parameters.Add(new SqlParameter("@DbPrincipal", SqlDbType.NVarChar, 128) { Value = DbPrincipal });

        var returnCode = new SqlParameter("@ReturnCode", SqlDbType.Int)
        {
            Direction = ParameterDirection.ReturnValue
        };
        command.Parameters.Add(returnCode);

        command.ExecuteNonQuery();

        ThrowIfNotGranted((int)returnCode.Value);
    }

    /// <summary>
    /// Interprets an sp_getapplock return code and throws unless the lock was granted.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>0 and 1: granted, immediately or after waiting.</item>
    /// <item>-1: timed out. Nothing was attempted, so a retry is safe. Reported as
    /// <see cref="ServiceUnavailableException"/>, never as a slot conflict.</item>
    /// <item>-2 (canceled), -3 (deadlock victim), -999 (call error) and any other negative code:
    /// unexpected failures, reported as <see cref="InvalidOperationException"/> (a generic server
    /// error at the API). SQL Server does not roll the transaction back for these codes, so the
    /// caller must.</item>
    /// </list>
    /// </remarks>
    public static void ThrowIfNotGranted(int returnCode)
    {
        if (returnCode >= 0)
        {
            return;
        }

        if (returnCode == -1)
        {
            throw new ServiceUnavailableException(BusyMessage);
        }

        throw new InvalidOperationException(
            $"The calendar lock was not granted: sp_getapplock returned {returnCode}.");
    }

    public static void EnsureValidTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.FromMilliseconds(1) || timeout > MaxTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                $"The calendar lock timeout must be between 1 ms and {MaxTimeout.TotalSeconds} s.");
        }
    }
}
