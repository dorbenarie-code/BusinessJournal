namespace BusinessJournal.Application.Common.Exceptions;

/// <summary>
/// The operation was not attempted because a required resource is temporarily busy.
/// Nothing was changed, and the request can be retried.
/// </summary>
/// <remarks>
/// The message is shown to API clients, so it must not contain internal details.
/// </remarks>
public sealed class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException(string message)
        : base(message)
    {
    }
}
