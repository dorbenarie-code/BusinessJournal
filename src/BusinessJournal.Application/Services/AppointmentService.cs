using BusinessJournal.Application.Common.Exceptions;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;

namespace BusinessJournal.Application.Services;

public sealed class AppointmentService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAppointmentRepository _appointmentRepository;

    public AppointmentService(
        ICustomerRepository customerRepository,
        IAppointmentRepository appointmentRepository)
    {
        ArgumentNullException.ThrowIfNull(customerRepository);
        ArgumentNullException.ThrowIfNull(appointmentRepository);

        _customerRepository = customerRepository;
        _appointmentRepository = appointmentRepository;
    }

    public Appointment ScheduleAppointment(
        Guid customerId,
        string title,
        DateTime startsAt,
        DateTime endsAt,
        string? notes = null)
    {
        var requestedTime = TimeRange.Create(startsAt, endsAt);

        return ScheduleAppointment(customerId, title, requestedTime, notes);
    }

    public Appointment? FindAppointmentById(Guid appointmentId)
    {
        return _appointmentRepository.FindById(appointmentId);
    }

    public IReadOnlyCollection<Appointment> GetAppointmentsForCustomer(Guid customerId)
    {
        return _appointmentRepository.GetByCustomerId(customerId);
    }

    public void CancelAppointment(Guid appointmentId)
    {
        var appointment = GetRequiredAppointment(appointmentId);

        appointment.Cancel();
        _appointmentRepository.Update(appointment);
    }

    public void RescheduleAppointment(Guid appointmentId, DateTime startsAt, DateTime endsAt)
    {
        var newTime = TimeRange.Create(startsAt, endsAt);

        RescheduleAppointment(appointmentId, newTime);
    }

    private Appointment ScheduleAppointment(
        Guid customerId,
        string title,
        TimeRange requestedTime,
        string? notes)
    {
        var customer = _customerRepository.FindById(customerId);
        if (customer is null)
        {
            throw new NotFoundException("Customer does not exist.");
        }

        var overlappingAppointments = _appointmentRepository.GetOverlapping(
            requestedTime.Start,
            requestedTime.End);

        if (overlappingAppointments.Any())
        {
            throw new ConflictException("The requested time slot is not available.");
        }

        var appointment = Appointment.Create(
            customerId,
            title,
            requestedTime,
            notes);

        _appointmentRepository.Add(appointment);

        return appointment;
    }

    private void RescheduleAppointment(Guid appointmentId, TimeRange newTime)
    {
        var appointment = GetRequiredAppointment(appointmentId);

        if (appointment.IsCancelled)
        {
            throw new ConflictException("Cancelled appointments cannot be rescheduled.");
        }

        var overlappingAppointments = _appointmentRepository.GetOverlapping(
            newTime.Start,
            newTime.End);

        var hasConflict = overlappingAppointments.Any(existing => existing.Id != appointment.Id);
        if (hasConflict)
        {
            throw new ConflictException("The requested time slot is not available.");
        }

        appointment.Reschedule(newTime);
        _appointmentRepository.Update(appointment);
    }

    private Appointment GetRequiredAppointment(Guid appointmentId)
    {
        var appointment = _appointmentRepository.FindById(appointmentId);
        if (appointment is null)
        {
            throw new NotFoundException("Appointment does not exist.");
        }

        return appointment;
    }
}