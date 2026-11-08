using System.Collections.Concurrent;
using BusinessJournal.Application.Contracts.Appointments;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;

namespace BusinessJournal.Infrastructure.Repositories;

public sealed class InMemoryAppointmentRepository : IAppointmentRepository
{
    // One lock for the whole calendar, like the SQL calendar lock: each change checks and
    // writes as a single step.
    private readonly object _calendarLock = new();
    private readonly ConcurrentDictionary<Guid, Appointment> _appointments = new();

    public bool TryAdd(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (appointment.IsCancelled)
        {
            throw new InvalidOperationException("Only active appointments can be added to the calendar.");
        }

        lock (_calendarLock)
        {
            if (_appointments.ContainsKey(appointment.Id))
            {
                throw new InvalidOperationException("An appointment with the same id already exists.");
            }

            if (HasActiveOverlap(appointment.Time, excludedAppointmentId: null))
            {
                return false;
            }

            _appointments[appointment.Id] = appointment;
            return true;
        }
    }

    public RescheduleAppointmentResult TryReschedule(Guid appointmentId, TimeRange newTime)
    {
        ArgumentNullException.ThrowIfNull(newTime);

        lock (_calendarLock)
        {
            if (!_appointments.TryGetValue(appointmentId, out var appointment))
            {
                return RescheduleAppointmentResult.NotFound;
            }

            if (appointment.IsCancelled)
            {
                return RescheduleAppointmentResult.Cancelled;
            }

            if (HasActiveOverlap(newTime, appointmentId))
            {
                return RescheduleAppointmentResult.SlotTaken;
            }

            appointment.Reschedule(newTime);
            return RescheduleAppointmentResult.Rescheduled;
        }
    }

    public bool TryCancel(Guid appointmentId)
    {
        lock (_calendarLock)
        {
            if (!_appointments.TryGetValue(appointmentId, out var appointment))
            {
                return false;
            }

            appointment.Cancel();
            return true;
        }
    }

    public Appointment? FindById(Guid appointmentId)
    {
        return _appointments.TryGetValue(appointmentId, out var appointment)
            ? appointment
            : null;
    }

    public IReadOnlyCollection<Appointment> GetByCustomerId(Guid customerId)
    {
        return _appointments.Values
            .Where(appointment => appointment.CustomerId == customerId)
            .OrderBy(appointment => appointment.StartsAt)
            .ThenBy(appointment => appointment.Id)
            .ToList();
    }

    public IReadOnlyCollection<Appointment> GetOverlapping(DateTime startsAt, DateTime endsAt)
    {
        var requestedTime = TimeRange.Create(startsAt, endsAt);

        return _appointments.Values
            .Where(appointment => !appointment.IsCancelled)
            .Where(appointment => appointment.OverlapsWith(requestedTime))
            .ToList();
    }

    private bool HasActiveOverlap(TimeRange time, Guid? excludedAppointmentId)
    {
        return _appointments.Values.Any(appointment =>
            appointment.Id != excludedAppointmentId
            && !appointment.IsCancelled
            && appointment.OverlapsWith(time));
    }
}