using BusinessJournal.Application.Contracts.Appointments;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;

namespace BusinessJournal.Application.Interfaces;

/// <summary>
/// Appointment storage for a single-provider calendar.
/// </summary>
/// <remarks>
/// <see cref="TryAdd"/>, <see cref="TryReschedule"/> and <see cref="TryCancel"/> are the only ways
/// to change the calendar. Each one checks its rules and writes as one atomic step, serialized with
/// every other calendar change, and reports success only after the change is durable.
/// Overlap uses half-open ranges [start, end): adjacent appointments do not overlap.
/// </remarks>
public interface IAppointmentRepository
{
    /// <summary>
    /// Adds an active appointment if no other active appointment overlaps its time range.
    /// </summary>
    /// <returns><c>false</c> when the slot is taken; nothing is written in that case.</returns>
    bool TryAdd(Appointment appointment);

    /// <summary>
    /// Moves an active appointment to <paramref name="newTime"/> if no other active appointment
    /// overlaps it. Only the time changes; the appointment's own current range is not a conflict.
    /// </summary>
    RescheduleAppointmentResult TryReschedule(Guid appointmentId, TimeRange newTime);

    /// <summary>
    /// Cancels the appointment. Cancelling an already cancelled appointment succeeds.
    /// Only the cancellation flag changes.
    /// </summary>
    /// <returns><c>false</c> when no appointment has this id.</returns>
    bool TryCancel(Guid appointmentId);

    Appointment? FindById(Guid appointmentId);

    IReadOnlyCollection<Appointment> GetByCustomerId(Guid customerId);

    /// <summary>
    /// Read-only snapshot of active appointments that overlap the range. The result can be stale
    /// as soon as it is returned, so it must not be used to decide whether a write is allowed.
    /// </summary>
    IReadOnlyCollection<Appointment> GetOverlapping(DateTime startsAt, DateTime endsAt);
}