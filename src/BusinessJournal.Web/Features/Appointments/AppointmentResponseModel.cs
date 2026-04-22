namespace BusinessJournal.Web.Features.Appointments;

public sealed class AppointmentResponseModel
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime StartsAt { get; init; }
    public DateTime EndsAt { get; init; }
    public string? Notes { get; init; }
    public bool IsCancelled { get; init; }
}