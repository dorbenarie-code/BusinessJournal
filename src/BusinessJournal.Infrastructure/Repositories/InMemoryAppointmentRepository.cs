using System.Collections.Concurrent;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;

namespace BusinessJournal.Infrastructure.Repositories;

public sealed class InMemoryAppointmentRepository : IAppointmentRepository
{
    private readonly ConcurrentDictionary<Guid, Appointment> _appointments = new();

    public void Add(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (!_appointments.TryAdd(appointment.Id, appointment))
        {
            throw new InvalidOperationException("An appointment with the same id already exists.");
        }
    }

    public void Update(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (!_appointments.ContainsKey(appointment.Id))
        {
            throw new InvalidOperationException("Appointment does not exist.");
        }

        _appointments[appointment.Id] = appointment;
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
}