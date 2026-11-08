using BusinessJournal.Application.Common.Exceptions;
using BusinessJournal.Application.Contracts.Appointments;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;

namespace BusinessJournal.Application.Services;

public sealed class AppointmentService
{
    private const string SlotNotAvailableMessage = "The requested time slot is not available.";
    private const string AppointmentNotFoundMessage = "Appointment does not exist.";

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
        // Idempotent: cancelling an already cancelled appointment succeeds.
        if (!_appointmentRepository.TryCancel(appointmentId))
        {
            throw new NotFoundException(AppointmentNotFoundMessage);
        }
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

        var appointment = Appointment.Create(
            customerId,
            title,
            requestedTime,
            notes);

        // The overlap check and the insert happen as one atomic step in the repository.
        if (!_appointmentRepository.TryAdd(appointment))
        {
            throw new ConflictException(SlotNotAvailableMessage);
        }

        return appointment;
    }

    private void RescheduleAppointment(Guid appointmentId, TimeRange newTime)
    {
        var result = _appointmentRepository.TryReschedule(appointmentId, newTime);

        switch (result)
        {
            case RescheduleAppointmentResult.Rescheduled:
                return;
            case RescheduleAppointmentResult.NotFound:
                throw new NotFoundException(AppointmentNotFoundMessage);
            case RescheduleAppointmentResult.Cancelled:
                throw new ConflictException("Cancelled appointments cannot be rescheduled.");
            case RescheduleAppointmentResult.SlotTaken:
                throw new ConflictException(SlotNotAvailableMessage);
            default:
                throw new InvalidOperationException($"Unknown reschedule result '{result}'.");
        }
    }
}