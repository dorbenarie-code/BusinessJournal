using BusinessJournal.Application.Common.Exceptions;
using BusinessJournal.Application.Contracts.Appointments;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Application.Services;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;
using BusinessJournal.Infrastructure.Repositories;
using Xunit;

namespace BusinessJournal.Tests.Application;

/// <summary>
/// The service hands each calendar change to the repository as one atomic call and maps
/// the repository's outcome to the existing application errors.
/// </summary>
public sealed class AppointmentServicePersistenceTests
{
    [Fact]
    public void CancelAppointment_ShouldCallRepositoryCancelOnceWithTheId()
    {
        var appointmentId = Guid.NewGuid();
        var appointmentRepository = new SpyAppointmentRepository { CancelResult = true };
        var service = new AppointmentService(new InMemoryCustomerRepository(), appointmentRepository);

        service.CancelAppointment(appointmentId);

        Assert.Equal(new[] { appointmentId }, appointmentRepository.CancelledIds);
    }

    [Fact]
    public void CancelAppointment_WhenRepositoryFindsNoAppointment_ShouldThrowNotFoundException()
    {
        var appointmentRepository = new SpyAppointmentRepository { CancelResult = false };
        var service = new AppointmentService(new InMemoryCustomerRepository(), appointmentRepository);

        var exception = Assert.Throws<NotFoundException>(() => service.CancelAppointment(Guid.NewGuid()));

        Assert.Equal("Appointment does not exist.", exception.Message);
    }

    [Fact]
    public void RescheduleAppointment_ShouldCallRepositoryRescheduleOnceWithTheNewTime()
    {
        var appointmentId = Guid.NewGuid();
        var appointmentRepository = new SpyAppointmentRepository
        {
            RescheduleResult = RescheduleAppointmentResult.Rescheduled
        };
        var service = new AppointmentService(new InMemoryCustomerRepository(), appointmentRepository);

        service.RescheduleAppointment(
            appointmentId,
            new DateTime(2026, 4, 10, 12, 0, 0),
            new DateTime(2026, 4, 10, 13, 0, 0));

        var call = Assert.Single(appointmentRepository.RescheduleCalls);
        Assert.Equal(appointmentId, call.AppointmentId);
        Assert.Equal(new DateTime(2026, 4, 10, 12, 0, 0), call.NewTime.Start);
        Assert.Equal(new DateTime(2026, 4, 10, 13, 0, 0), call.NewTime.End);
    }

    [Theory]
    [InlineData(RescheduleAppointmentResult.NotFound, typeof(NotFoundException), "Appointment does not exist.")]
    [InlineData(RescheduleAppointmentResult.Cancelled, typeof(ConflictException), "Cancelled appointments cannot be rescheduled.")]
    [InlineData(RescheduleAppointmentResult.SlotTaken, typeof(ConflictException), "The requested time slot is not available.")]
    public void RescheduleAppointment_WhenRepositoryRejects_ShouldThrowTheMatchingApplicationError(
        RescheduleAppointmentResult result,
        Type expectedException,
        string expectedMessage)
    {
        var appointmentRepository = new SpyAppointmentRepository { RescheduleResult = result };
        var service = new AppointmentService(new InMemoryCustomerRepository(), appointmentRepository);

        var exception = Assert.Throws(expectedException, () =>
            service.RescheduleAppointment(
                Guid.NewGuid(),
                new DateTime(2026, 4, 10, 12, 0, 0),
                new DateTime(2026, 4, 10, 13, 0, 0)));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void RescheduleAppointment_WhenRepositoryReturnsUnknownResult_ShouldNotReportSuccess()
    {
        var appointmentRepository = new SpyAppointmentRepository { RescheduleResult = default };
        var service = new AppointmentService(new InMemoryCustomerRepository(), appointmentRepository);

        Assert.Throws<InvalidOperationException>(() =>
            service.RescheduleAppointment(
                Guid.NewGuid(),
                new DateTime(2026, 4, 10, 12, 0, 0),
                new DateTime(2026, 4, 10, 13, 0, 0)));
    }

    [Fact]
    public void ScheduleAppointment_ShouldAddThroughTheAtomicRepositoryCallOnly()
    {
        var customerRepository = new InMemoryCustomerRepository();
        var customer = new CustomerService(customerRepository).RegisterCustomer("Rachel Cohen", "0501234567");
        var appointmentRepository = new SpyAppointmentRepository { AddResult = true };
        var service = new AppointmentService(customerRepository, appointmentRepository);

        var appointment = service.ScheduleAppointment(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 10, 10, 0, 0),
            new DateTime(2026, 4, 10, 11, 0, 0));

        Assert.Same(appointment, Assert.Single(appointmentRepository.AddedAppointments));
        Assert.Equal(0, appointmentRepository.GetOverlappingCallCount);
    }

    [Fact]
    public void ScheduleAppointment_WhenRepositoryReportsSlotTaken_ShouldThrowConflictException()
    {
        var customerRepository = new InMemoryCustomerRepository();
        var customer = new CustomerService(customerRepository).RegisterCustomer("Rachel Cohen", "0501234567");
        var appointmentRepository = new SpyAppointmentRepository { AddResult = false };
        var service = new AppointmentService(customerRepository, appointmentRepository);

        var exception = Assert.Throws<ConflictException>(() =>
            service.ScheduleAppointment(
                customer.Id,
                "Hair Color",
                new DateTime(2026, 4, 10, 10, 0, 0),
                new DateTime(2026, 4, 10, 11, 0, 0)));

        Assert.Equal("The requested time slot is not available.", exception.Message);
    }

    private sealed class SpyAppointmentRepository : IAppointmentRepository
    {
        public bool AddResult { get; init; }
        public bool CancelResult { get; init; }
        public RescheduleAppointmentResult RescheduleResult { get; init; }

        public List<Appointment> AddedAppointments { get; } = new();
        public List<Guid> CancelledIds { get; } = new();
        public List<(Guid AppointmentId, TimeRange NewTime)> RescheduleCalls { get; } = new();
        public int GetOverlappingCallCount { get; private set; }

        public bool TryAdd(Appointment appointment)
        {
            AddedAppointments.Add(appointment);
            return AddResult;
        }

        public RescheduleAppointmentResult TryReschedule(Guid appointmentId, TimeRange newTime)
        {
            RescheduleCalls.Add((appointmentId, newTime));
            return RescheduleResult;
        }

        public bool TryCancel(Guid appointmentId)
        {
            CancelledIds.Add(appointmentId);
            return CancelResult;
        }

        public Appointment? FindById(Guid appointmentId)
        {
            return null;
        }

        public IReadOnlyCollection<Appointment> GetByCustomerId(Guid customerId)
        {
            return Array.Empty<Appointment>();
        }

        public IReadOnlyCollection<Appointment> GetOverlapping(DateTime startsAt, DateTime endsAt)
        {
            GetOverlappingCallCount++;
            return Array.Empty<Appointment>();
        }
    }
}
