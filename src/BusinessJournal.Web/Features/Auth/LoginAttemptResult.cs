namespace BusinessJournal.Web.Features.Auth;

public sealed class LoginAttemptResult
{
    private LoginAttemptResult(
        bool isSuccess,
        LoginResponseModel? response,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        Response = response;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public LoginResponseModel? Response { get; }
    public string? ErrorMessage { get; }

    public static LoginAttemptResult Success(LoginResponseModel response) =>
        new(true, response, null);

    public static LoginAttemptResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}