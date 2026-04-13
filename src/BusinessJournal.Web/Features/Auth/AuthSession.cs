namespace BusinessJournal.Web.Features.Auth;

public sealed class AuthSession
{
    public string? AccessToken { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AccessToken)
        && ExpiresAtUtc > DateTime.UtcNow;

    public void SetSession(string accessToken, DateTime expiresAtUtc)
    {
        AccessToken = accessToken;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void Clear()
    {
        AccessToken = null;
        ExpiresAtUtc = default;
    }
}