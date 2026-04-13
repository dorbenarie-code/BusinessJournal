using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;
using Xunit;

namespace BusinessJournal.Tests.Domain;

public sealed class AppointmentTests
{
    [Fact]
    public void Create_WithValidValues_ShouldCreateAppointment()
    {
        var customerId = Guid.NewGuid();
        var time = TimeRange.Create(
            new DateTime(2026, 4, 6, 10, 0, 0),
            new DateTime(2026, 4, 6, 11, 0, 0));

        var appointment = Appointment.Create(
            customerId,
            "Hair Color",
            time,
            "First visit");

        Assert.NotEqual(Guid.Empty, appointment.Id);
        Assert.Equal(customerId, appointment.CustomerId);
        Assert.Equal("Hair Color", appointment.Title);
        Assert.Equal(time, appointment.Time);
        Assert.Equal(time.Start, appointment.StartsAt);
        Assert.Equal(time.End, appointment.EndsAt);
        Assert.Equal("First visit", appointment.Notes);
        Assert.False(appointment.IsCancelled);
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ShouldThrowArgumentException()
    {
        var time = TimeRange.Create(
            new DateTime(2026, 4, 6, 10, 0, 0),
            new DateTime(2026, 4, 6, 11, 0, 0));

        Assert.Throws<ArgumentException>(() =>
            Appointment.Create(Guid.Empty, "Hair Color", time));
    }

    [Fact]
    public void Create_WithEmptyTitle_ShouldThrowArgumentException()
    {
        var customerId = Guid.NewGuid();
        var time = TimeRange.Create(
            new DateTime(2026, 4, 6, 10, 0, 0),
            new DateTime(2026, 4, 6, 11, 0, 0));

        Assert.Throws<ArgumentException>(() =>
            Appointment.Create(customerId, "   ", time));
    }

    [Fact]
    public void Create_WithInvalidTimeRange_ShouldThrowArgumentException()
    {
        var customerId = Guid.NewGuid();
        var startsAt = new DateTime(2026, 4, 6, 11, 0, 0);
        var endsAt = new DateTime(2026, 4, 6, 10, 0, 0);

        Assert.Throws<ArgumentException>(() =>
            Appointment.Create(customerId, "Hair Color", startsAt, endsAt));
    }

    [Fact]
    public void ChangeTitle_WithValidValue_ShouldUpdateTitle()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            TimeRange.Create(
                new DateTime(2026, 4, 6, 10, 0, 0),
                new DateTime(2026, 4, 6, 11, 0, 0)));

        appointment.ChangeTitle("Hair Cut");

        Assert.Equal("Hair Cut", appointment.Title);
    }

    [Fact]
    public void ChangeNotes_WithWhitespace_ShouldSetNotesToNull()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            TimeRange.Create(
                new DateTime(2026, 4, 6, 10, 0, 0),
                new DateTime(2026, 4, 6, 11, 0, 0)),
            "Some notes");

        appointment.ChangeNotes("   ");

        Assert.Null(appointment.Notes);
    }

    [Fact]
    public void Reschedule_WithValidRange_ShouldUpdateTime()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            TimeRange.Create(
                new DateTime(2026, 4, 6, 10, 0, 0),
                new DateTime(2026, 4, 6, 11, 0, 0)));

        var newTime = TimeRange.Create(
            new DateTime(2026, 4, 6, 12, 0, 0),
            new DateTime(2026, 4, 6, 13, 0, 0));

        appointment.Reschedule(newTime);

        Assert.Equal(newTime, appointment.Time);
        Assert.Equal(newTime.Start, appointment.StartsAt);
        Assert.Equal(newTime.End, appointment.EndsAt);
    }

    [Fact]
    public void Reschedule_WithInvalidRange_ShouldThrowArgumentException()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            TimeRange.Create(
                new DateTime(2026, 4, 6, 10, 0, 0),
                new DateTime(2026, 4, 6, 11, 0, 0)));

        Assert.Throws<ArgumentException>(() =>
            appointment.Reschedule(
                new DateTime(2026, 4, 6, 15, 0, 0),
                new DateTime(2026, 4, 6, 14, 0, 0)));
    }

    [Fact]
    public void Cancel_ShouldMarkAppointmentAsCancelled()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            TimeRange.Create(
                new DateTime(2026, 4, 6, 10, 0, 0),
                new DateTime(2026, 4, 6, 11, 0, 0)));

        appointment.Cancel();

        Assert.True(appointment.IsCancelled);
    }

    [Fact]
    public void OverlapsWith_WhenRangesOverlap_ShouldReturnTrue()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            TimeRange.Create(
                new DateTime(2026, 4, 7, 10, 0, 0),
                new DateTime(2026, 4, 7, 11, 0, 0)));

        var result = appointment.OverlapsWith(
            TimeRange.Create(
                new DateTime(2026, 4, 7, 10, 30, 0),
                new DateTime(2026, 4, 7, 11, 30, 0)));

        Assert.True(result);
    }

    [Fact]
    public void OverlapsWith_WhenRangesDoNotOverlap_ShouldReturnFalse()
    {
        var appointment = Appointment.Create(
            Guid.NewGuid(),
            "Hair Color",
            TimeRange.Create(
                new DateTime(2026, 4, 7, 10, 0, 0),
                new DateTime(2026, 4, 7, 11, 0, 0)));

        var result = appointment.OverlapsWith(
            TimeRange.Create(
                new DateTime(2026, 4, 7, 11, 0, 0),
                new DateTime(2026, 4, 7, 12, 0, 0)));

        Assert.False(result);
    }
}