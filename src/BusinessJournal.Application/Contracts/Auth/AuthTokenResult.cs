namespace BusinessJournal.Application.Contracts.Auth;

public sealed record AuthTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc);