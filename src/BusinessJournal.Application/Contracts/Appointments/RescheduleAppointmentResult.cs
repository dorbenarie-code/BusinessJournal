namespace BusinessJournal.Application.Contracts.Appointments;

/// <summary>
/// Outcome of an atomic reschedule. Only <see cref="Rescheduled"/> means the change was saved.
/// </summary>
/// <remarks>
/// No member is zero, so an uninitialized value is never mistaken for success.
/// </remarks>
public enum RescheduleAppointmentResult
{
    Rescheduled = 1,
    NotFound = 2,
    Cancelled = 3,
    SlotTaken = 4
}
