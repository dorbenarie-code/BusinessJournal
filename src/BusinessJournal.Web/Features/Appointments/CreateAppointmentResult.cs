namespace BusinessJournal.Web.Features.Appointments;

public sealed class CreateAppointmentResult
{
    private CreateAppointmentResult(
        bool isSuccess,
        AppointmentResponseModel? appointment,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        Appointment = appointment;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public AppointmentResponseModel? Appointment { get; }
    public string? ErrorMessage { get; }

    public static CreateAppointmentResult Success(AppointmentResponseModel appointment) =>
        new(true, appointment, null);

    public static CreateAppointmentResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}