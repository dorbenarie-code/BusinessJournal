namespace BusinessJournal.Web.Features.Auth;

public sealed class LoginResponseModel
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}