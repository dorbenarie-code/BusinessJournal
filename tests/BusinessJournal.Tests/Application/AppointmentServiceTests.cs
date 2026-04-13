using BusinessJournal.Application.Common.Exceptions;
using BusinessJournal.Application.Services;
using BusinessJournal.Infrastructure.Repositories;
using Xunit;

namespace BusinessJournal.Tests.Application;

public sealed class AppointmentServiceTests
{
    [Fact]
    public void ScheduleAppointment_WithValidValues_ShouldCreateAndStoreAppointment()
    {
        var customerRepository = new InMemoryCustomerRepository();
        var appointmentRepository = new InMemoryAppointmentRepository();
        var customerService = new CustomerService(customerRepository);
        var appointmentService = new AppointmentService(customerRepository, appointmentRepository);

        var customer = customerService.RegisterCustomer("Rachel Cohen", "0501234567");

        var appointment = appointmentService.ScheduleAppointment(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 8, 10, 0, 0),
            new DateTime(2026, 4, 8, 11, 0, 0),
            "First visit");

        Assert.NotNull(appointment);

        var storedAppointment = appointmentRepository.FindById(appointment.Id);

        Assert.NotNull(storedAppointment);
        Assert.Equal(customer.Id, storedAppointment!.CustomerId);
        Assert.Equal("Hair Color", storedAppointment.Title);
    }

    [Fact]
    public void ScheduleAppointment_WhenCustomerDoesNotExist_ShouldThrowNotFoundException()
    {
        var customerRepository = new InMemoryCustomerRepository();
        var appointmentRepository = new InMemoryAppointmentRepository();
        var appointmentService = new AppointmentService(customerRepository, appointmentRepository);

        Assert.Throws<NotFoundException>(() =>
            appointmentService.ScheduleAppointment(
                Guid.NewGuid(),
                "Hair Color",
                new DateTime(2026, 4, 8, 10, 0, 0),
                new DateTime(2026, 4, 8, 11, 0, 0)));
    }

    [Fact]
    public void ScheduleAppointment_WhenTimeSlotIsTaken_ShouldThrowConflictException()
    {
        var customerRepository = new InMemoryCustomerRepository();
        var appointmentRepository = new InMemoryAppointmentRepository();
        var customerService = new CustomerService(customerRepository);
        var appointmentService = new AppointmentService(customerRepository, appointmentRepository);

        var customer = customerService.RegisterCustomer("Rachel Cohen", "0501234567");

        appointmentService.ScheduleAppointment(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 8, 10, 0, 0),
            new DateTime(2026, 4, 8, 11, 0, 0));

        Assert.Throws<ConflictException>(() =>
            appointmentService.ScheduleAppointment(
                customer.Id,
                "Hair Cut",
                new DateTime(2026, 4, 8, 10, 30, 0),
                new DateTime(2026, 4, 8, 11, 30, 0)));
    }

    [Fact]
    public void CancelAppointment_WhenAppointmentExists_ShouldMarkItAsCancelled()
    {
        var customerRepository = new InMemoryCustomerRepository();
        var appointmentRepository = new InMemoryAppointmentRepository();
        var customerService = new CustomerService(customerRepository);
        var appointmentService = new AppointmentService(customerRepository, appointmentRepository);

        var customer = customerService.RegisterCustomer("Rachel Cohen", "0501234567");

        var appointment = appointmentService.ScheduleAppointment(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 8, 10, 0, 0),
            new DateTime(2026, 4, 8, 11, 0, 0));

        appointmentService.CancelAppointment(appointment.Id);

        var storedAppointment = appointmentRepository.FindById(appointment.Id);

        Assert.NotNull(storedAppointment);
        Assert.True(storedAppointment!.IsCancelled);
    }

    [Fact]
    public void CancelAppointment_WhenAppointmentDoesNotExist_ShouldThrowNotFoundException()
    {
        var customerRepository = new InMemoryCustomerRepository();
        var appointmentRepository = new InMemoryAppointmentRepository();
        var appointmentService = new AppointmentService(customerRepository, appointmentRepository);

        Assert.Throws<NotFoundException>(() =>
            appointmentService.CancelAppointment(Guid.NewGuid()));
    }

    [Fact]
    public void RescheduleAppointment_WithAvailableTimeSlot_ShouldUpdateAppointmentTime()
    {
        var customerRepository = new InMemoryCustomerRepository();
        var appointmentRepository = new InMemoryAppointmentRepository();
        var customerService = new CustomerService(customerRepository);
        var appointmentService = new AppointmentService(customerRepository, appointmentRepository);

        var customer = customerService.RegisterCustomer("Rachel Cohen", "0501234567");

        var appointment = appointmentService.ScheduleAppointment(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 8, 10, 0, 0),
            new DateTime(2026, 4, 8, 11, 0, 0));

        appointmentService.RescheduleAppointment(
            appointment.Id,
            new DateTime(2026, 4, 8, 12, 0, 0),
            new DateTime(2026, 4, 8, 13, 0, 0));

        var updatedAppointment = appointmentRepository.FindById(appointment.Id);

        Assert.NotNull(updatedAppointment);
        Assert.Equal(new DateTime(2026, 4, 8, 12, 0, 0), updatedAppointment!.StartsAt);
        Assert.Equal(new DateTime(2026, 4, 8, 13, 0, 0), updatedAppointment.EndsAt);
    }

    [Fact]
    public void RescheduleAppointment_WhenAppointmentIsCancelled_ShouldThrowConflictException()
    {
        var customerRepository = new InMemoryCustomerRepository();
        var appointmentRepository = new InMemoryAppointmentRepository();
        var customerService = new CustomerService(customerRepository);
        var appointmentService = new AppointmentService(customerRepository, appointmentRepository);

        var customer = customerService.RegisterCustomer("Rachel Cohen", "0501234567");

        var appointment = appointmentService.ScheduleAppointment(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 8, 10, 0, 0),
            new DateTime(2026, 4, 8, 11, 0, 0));

        appointmentService.CancelAppointment(appointment.Id);

        Assert.Throws<ConflictException>(() =>
            appointmentService.RescheduleAppointment(
                appointment.Id,
                new DateTime(2026, 4, 8, 12, 0, 0),
                new DateTime(2026, 4, 8, 13, 0, 0)));
    }

    [Fact]
    public void RescheduleAppointment_WhenTimeSlotIsTaken_ShouldThrowConflictException()
    {
        var customerRepository = new InMemoryCustomerRepository();
        var appointmentRepository = new InMemoryAppointmentRepository();
        var customerService = new CustomerService(customerRepository);
        var appointmentService = new AppointmentService(customerRepository, appointmentRepository);

        var firstCustomer = customerService.RegisterCustomer("Rachel Cohen", "0501234567");
        var secondCustomer = customerService.RegisterCustomer("Michal Levi", "0521234567");

        var firstAppointment = appointmentService.ScheduleAppointment(
            firstCustomer.Id,
            "Hair Color",
            new DateTime(2026, 4, 8, 10, 0, 0),
            new DateTime(2026, 4, 8, 11, 0, 0));

        appointmentService.ScheduleAppointment(
            secondCustomer.Id,
            "Hair Cut",
            new DateTime(2026, 4, 8, 12, 0, 0),
            new DateTime(2026, 4, 8, 13, 0, 0));

        Assert.Throws<ConflictException>(() =>
            appointmentService.RescheduleAppointment(
                firstAppointment.Id,
                new DateTime(2026, 4, 8, 12, 30, 0),
                new DateTime(2026, 4, 8, 13, 30, 0)));
    }
}