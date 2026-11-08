using BusinessJournal.Application.Contracts.Appointments;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;
using BusinessJournal.Infrastructure.Repositories;
using Xunit;

namespace BusinessJournal.Tests.Infrastructure;

public sealed class InMemoryAppointmentRepositoryTests
{
    [Fact]
    public void TryAdd_WithValidAppointment_ShouldStoreAppointment()
    {
        var repository = new InMemoryAppointmentRepository();
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            new DateTime(2026, 4, 7, 10, 0, 0),
            new DateTime(2026, 4, 7, 11, 0, 0));

        Assert.True(repository.TryAdd(appointment));

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

    Assert.True(repository.TryAdd(laterAppointment));
    Assert.True(repository.TryAdd(earlierAppointment));
    Assert.True(repository.TryAdd(otherAppointment));

    var appointments = repository.GetByCustomerId(customerId).ToList();

    Assert.Equal(2, appointments.Count);

    Assert.Equal(earlierAppointment.Id, appointments[0].Id);
    Assert.Equal(laterAppointment.Id, appointments[1].Id);

    Assert.DoesNotContain(appointments, appointment => appointment.Id == otherAppointment.Id);
}

    [Fact]
    public void TryAdd_WhenSameAppointmentIsAddedTwice_ShouldThrowInvalidOperationException()
    {
        var repository = new InMemoryAppointmentRepository();
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            new DateTime(2026, 4, 7, 10, 0, 0),
            new DateTime(2026, 4, 7, 11, 0, 0));

        Assert.True(repository.TryAdd(appointment));

        Assert.Throws<InvalidOperationException>(() => repository.TryAdd(appointment));
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

    var nonOverlappingAppointment = Appointment.Create(
        Guid.NewGuid(),
        "Nails",
        new DateTime(2026, 4, 7, 12, 0, 0),
        new DateTime(2026, 4, 7, 13, 0, 0));

    Assert.True(repository.TryAdd(cancelledAppointment));
    Assert.True(repository.TryCancel(cancelledAppointment.Id));
    Assert.True(repository.TryAdd(overlappingAppointment));
    Assert.True(repository.TryAdd(nonOverlappingAppointment));

    var result = repository.GetOverlapping(
        new DateTime(2026, 4, 7, 10, 30, 0),
        new DateTime(2026, 4, 7, 11, 30, 0));

    Assert.Single(result);
    Assert.Equal(overlappingAppointment.Id, result.Single().Id);
}

    [Fact]
    public void TryAdd_WhenSlotOverlapsActiveAppointment_ShouldReturnFalseAndNotStore()
    {
        var repository = new InMemoryAppointmentRepository();
        var existing = CreateAppointment(10, 0, 11, 0);
        var overlapping = CreateAppointment(10, 30, 11, 30);

        Assert.True(repository.TryAdd(existing));

        Assert.False(repository.TryAdd(overlapping));
        Assert.Null(repository.FindById(overlapping.Id));
    }

    [Fact]
    public void TryAdd_WhenAdjacentToActiveAppointment_ShouldSucceed()
    {
        var repository = new InMemoryAppointmentRepository();

        Assert.True(repository.TryAdd(CreateAppointment(10, 0, 11, 0)));
        Assert.True(repository.TryAdd(CreateAppointment(11, 0, 12, 0)));
        Assert.True(repository.TryAdd(CreateAppointment(9, 0, 10, 0)));
    }

    [Fact]
    public void TryAdd_WhenAppointmentIsCancelled_ShouldThrowAndNotStore()
    {
        var repository = new InMemoryAppointmentRepository();
        var appointment = CreateAppointment(10, 0, 11, 0);
        appointment.Cancel();

        Assert.Throws<InvalidOperationException>(() => repository.TryAdd(appointment));
        Assert.Null(repository.FindById(appointment.Id));
    }

    [Fact]
    public void TryAdd_WhenCalledInParallelForSameSlot_ShouldStoreExactlyOne()
    {
        var repository = new InMemoryAppointmentRepository();
        var candidates = Enumerable.Range(0, 50)
            .Select(_ => CreateAppointment(10, 0, 11, 0))
            .ToArray();
        var results = new bool[candidates.Length];

        Parallel.For(0, candidates.Length, index => results[index] = repository.TryAdd(candidates[index]));

        Assert.Equal(1, results.Count(added => added));
        Assert.Single(repository.GetOverlapping(Day.AddHours(10), Day.AddHours(11)));
    }

    [Fact]
    public void TryReschedule_WhenAppointmentDoesNotExist_ShouldReturnNotFound()
    {
        var repository = new InMemoryAppointmentRepository();

        var result = repository.TryReschedule(Guid.NewGuid(), Range(12, 0, 13, 0));

        Assert.Equal(RescheduleAppointmentResult.NotFound, result);
    }

    [Fact]
    public void TryReschedule_WhenAppointmentIsCancelled_ShouldReturnCancelledAndKeepTime()
    {
        var repository = new InMemoryAppointmentRepository();
        var appointment = CreateAppointment(10, 0, 11, 0);
        Assert.True(repository.TryAdd(appointment));
        Assert.True(repository.TryCancel(appointment.Id));

        var result = repository.TryReschedule(appointment.Id, Range(12, 0, 13, 0));

        Assert.Equal(RescheduleAppointmentResult.Cancelled, result);
        var stored = repository.FindById(appointment.Id)!;
        Assert.True(stored.IsCancelled);
        Assert.Equal(Range(10, 0, 11, 0), stored.Time);
    }

    [Fact]
    public void TryReschedule_WhenNewTimeOverlapsAnotherActiveAppointment_ShouldReturnSlotTakenAndKeepTime()
    {
        var repository = new InMemoryAppointmentRepository();
        var appointment = CreateAppointment(10, 0, 11, 0);
        Assert.True(repository.TryAdd(appointment));
        Assert.True(repository.TryAdd(CreateAppointment(12, 0, 13, 0)));

        var result = repository.TryReschedule(appointment.Id, Range(12, 30, 13, 30));

        Assert.Equal(RescheduleAppointmentResult.SlotTaken, result);
        Assert.Equal(Range(10, 0, 11, 0), repository.FindById(appointment.Id)!.Time);
    }

    [Fact]
    public void TryReschedule_WhenNewTimeOverlapsOnlyItsOwnCurrentTime_ShouldReschedule()
    {
        var repository = new InMemoryAppointmentRepository();
        var appointment = CreateAppointment(10, 0, 11, 0);
        Assert.True(repository.TryAdd(appointment));

        var result = repository.TryReschedule(appointment.Id, Range(10, 30, 11, 30));

        Assert.Equal(RescheduleAppointmentResult.Rescheduled, result);
        Assert.Equal(Range(10, 30, 11, 30), repository.FindById(appointment.Id)!.Time);
    }

    [Fact]
    public void TryCancel_WhenAppointmentDoesNotExist_ShouldReturnFalse()
    {
        var repository = new InMemoryAppointmentRepository();

        Assert.False(repository.TryCancel(Guid.NewGuid()));
    }

    [Fact]
    public void TryCancel_WhenCalledTwice_ShouldSucceedBothTimes()
    {
        var repository = new InMemoryAppointmentRepository();
        var appointment = CreateAppointment(10, 0, 11, 0);
        Assert.True(repository.TryAdd(appointment));

        Assert.True(repository.TryCancel(appointment.Id));
        Assert.True(repository.TryCancel(appointment.Id));
        Assert.True(repository.FindById(appointment.Id)!.IsCancelled);
    }

    private static readonly DateTime Day = new(2026, 4, 7);

    private static TimeRange Range(int startHour, int startMinute, int endHour, int endMinute)
    {
        return TimeRange.Create(
            Day.AddHours(startHour).AddMinutes(startMinute),
            Day.AddHours(endHour).AddMinutes(endMinute));
    }

    private static Appointment CreateAppointment(int startHour, int startMinute, int endHour, int endMinute)
    {
        return Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            Range(startHour, startMinute, endHour, endMinute));
    }
}
