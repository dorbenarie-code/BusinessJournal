using BusinessJournal.Domain.Entities;
using BusinessJournal.Infrastructure.Repositories;
using Xunit;

namespace BusinessJournal.Tests.Infrastructure;

public sealed class InMemoryAppointmentRepositoryTests
{
    [Fact]
    public void Add_WithValidAppointment_ShouldStoreAppointment()
    {
        var repository = new InMemoryAppointmentRepository();
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            new DateTime(2026, 4, 7, 10, 0, 0),
            new DateTime(2026, 4, 7, 11, 0, 0));

        repository.Add(appointment);

        var storedAppointment = repository.FindById(appointment.Id);

        Assert.NotNull(storedAppointment);
        Assert.Equal(appointment.Id, storedAppointment!.Id);
    }

    [Fact]
    public void FindById_WhenAppointmentDoesNotExist_ShouldReturnNull()
    {
        var repository = new InMemoryAppointmentRepository();

        var appointment = repository.FindById(Guid.NewGuid());

        Assert.Null(appointment);
    }

    [Fact]
public void GetByCustomerId_ShouldReturnAppointmentsOfRequestedCustomerInStartsAtThenIdOrder()
{
    var repository = new InMemoryAppointmentRepository();

    var customerId = Guid.NewGuid();
    var otherCustomerId = Guid.NewGuid();

    var laterAppointment = Appointment.Create(
        customerId,
        "Hair Cut",
        new DateTime(2026, 4, 7, 12, 0, 0),
        new DateTime(2026, 4, 7, 13, 0, 0));

    var earlierAppointment = Appointment.Create(
        customerId,
        "Hair Color",
        new DateTime(2026, 4, 7, 10, 0, 0),
        new DateTime(2026, 4, 7, 11, 0, 0));

    var otherAppointment = Appointment.Create(
        otherCustomerId,
        "Nails",
        new DateTime(2026, 4, 7, 9, 0, 0),
        new DateTime(2026, 4, 7, 10, 0, 0));

    repository.Add(laterAppointment);
    repository.Add(earlierAppointment);
    repository.Add(otherAppointment);

    var appointments = repository.GetByCustomerId(customerId).ToList();

    Assert.Equal(2, appointments.Count);

    Assert.Equal(earlierAppointment.Id, appointments[0].Id);
    Assert.Equal(laterAppointment.Id, appointments[1].Id);

    Assert.DoesNotContain(appointments, appointment => appointment.Id == otherAppointment.Id);
}

    [Fact]
    public void Add_WhenSameAppointmentIsAddedTwice_ShouldThrowInvalidOperationException()
    {
        var repository = new InMemoryAppointmentRepository();
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            new DateTime(2026, 4, 7, 10, 0, 0),
            new DateTime(2026, 4, 7, 11, 0, 0));

        repository.Add(appointment);

        Assert.Throws<InvalidOperationException>(() => repository.Add(appointment));
    }

    [Fact]
public void GetOverlapping_ShouldReturnOnlyActiveOverlappingAppointments()
{
    var repository = new InMemoryAppointmentRepository();

    var overlappingAppointment = Appointment.Create(
        Guid.NewGuid(),
        "Hair Color",
        new DateTime(2026, 4, 7, 10, 0, 0),
        new DateTime(2026, 4, 7, 11, 0, 0));

    var cancelledAppointment = Appointment.Create(
        Guid.NewGuid(),
        "Cancelled",
        new DateTime(2026, 4, 7, 10, 15, 0),
        new DateTime(2026, 4, 7, 10, 45, 0));

    cancelledAppointment.Cancel();

    var nonOverlappingAppointment = Appointment.Create(
        Guid.NewGuid(),
        "Nails",
        new DateTime(2026, 4, 7, 12, 0, 0),
        new DateTime(2026, 4, 7, 13, 0, 0));

    repository.Add(overlappingAppointment);
    repository.Add(cancelledAppointment);
    repository.Add(nonOverlappingAppointment);

    var result = repository.GetOverlapping(
        new DateTime(2026, 4, 7, 10, 30, 0),
        new DateTime(2026, 4, 7, 11, 30, 0));

    Assert.Single(result);
    Assert.Equal(overlappingAppointment.Id, result.Single().Id);
}

}
