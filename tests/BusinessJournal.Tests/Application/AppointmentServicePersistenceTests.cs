using BusinessJournal.Application.Interfaces;
using BusinessJournal.Application.Services;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Infrastructure.Repositories;
using Xunit;

namespace BusinessJournal.Tests.Application;

public sealed class AppointmentServicePersistenceTests
{
    [Fact]
    public void CancelAppointment_ShouldCallRepositoryUpdate()
    {
        var customerRepository = new InMemoryCustomerRepository();

        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            new DateTime(2026, 4, 10, 10, 0, 0),
            new DateTime(2026, 4, 10, 11, 0, 0));

        var appointmentRepository = new SpyAppointmentRepository(appointment);
        var service = new AppointmentService(customerRepository, appointmentRepository);

        service.CancelAppointment(appointment.Id);

        Assert.Equal(1, appointmentRepository.UpdateCallCount);
        Assert.NotNull(appointmentRepository.UpdatedAppointment);
        Assert.True(appointmentRepository.UpdatedAppointment!.IsCancelled);
    }

    [Fact]
    public void RescheduleAppointment_ShouldCallRepositoryUpdate()
    {
        var customerRepository = new InMemoryCustomerRepository();

        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            new DateTime(2026, 4, 10, 10, 0, 0),
            new DateTime(2026, 4, 10, 11, 0, 0));

        var appointmentRepository = new SpyAppointmentRepository(appointment);
        var service = new AppointmentService(customerRepository, appointmentRepository);

        service.RescheduleAppointment(
            appointment.Id,
            new DateTime(2026, 4, 10, 12, 0, 0),
            new DateTime(2026, 4, 10, 13, 0, 0));

        Assert.Equal(1, appointmentRepository.UpdateCallCount);
        Assert.NotNull(appointmentRepository.UpdatedAppointment);
        Assert.Equal(new DateTime(2026, 4, 10, 12, 0, 0), appointmentRepository.UpdatedAppointment!.StartsAt);
        Assert.Equal(new DateTime(2026, 4, 10, 13, 0, 0), appointmentRepository.UpdatedAppointment.EndsAt);
    }

    private sealed class SpyAppointmentRepository : IAppointmentRepository
    {
        private readonly Appointment _appointment;

        public SpyAppointmentRepository(Appointment appointment)
        {
            _appointment = appointment;
        }

        public int UpdateCallCount { get; private set; }
        public Appointment? UpdatedAppointment { get; private set; }

        public void Add(Appointment appointment)
        {
        }

        public void Update(Appointment appointment)
        {
            UpdateCallCount++;
            UpdatedAppointment = appointment;
        }

        public Appointment? FindById(Guid appointmentId)
        {
            return _appointment.Id == appointmentId
                ? _appointment
                : null;
        }

        public IReadOnlyCollection<Appointment> GetByCustomerId(Guid customerId)
        {
            return Array.Empty<Appointment>();
        }

        public IReadOnlyCollection<Appointment> GetOverlapping(DateTime startsAt, DateTime endsAt)
        {
            return Array.Empty<Appointment>();
        }
    }
}
