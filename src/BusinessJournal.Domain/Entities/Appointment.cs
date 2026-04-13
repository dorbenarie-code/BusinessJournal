using System;
using BusinessJournal.Domain.Common;
using BusinessJournal.Domain.ValueObjects;

namespace BusinessJournal.Domain.Entities;

public sealed class Appointment
{
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public string Title { get; private set; }
    public TimeRange Time { get; private set; }
    public DateTime StartsAt => Time.Start;
    public DateTime EndsAt => Time.End;
    public string? Notes { get; private set; }
    public bool IsCancelled { get; private set; }

    private Appointment(
        Guid id,
        Guid customerId,
        string title,
        TimeRange time,
        string? notes)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Appointment id is required.", nameof(id));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        ArgumentNullException.ThrowIfNull(time);

        Id = id;
        CustomerId = customerId;
        Title = TextNormalizer.NormalizeRequired(title, nameof(title), "Title is required.");
        Time = time;
        Notes = TextNormalizer.NormalizeOptional(notes);
    }

    public static Appointment Create(
        Guid customerId,
        string title,
        TimeRange time,
        string? notes = null)
    {
        return new Appointment(
            Guid.NewGuid(),
            customerId,
            title,
            time,
            notes);
    }

    public static Appointment Create(
        Guid customerId,
        string title,
        DateTime startsAt,
        DateTime endsAt,
        string? notes = null)
    {
        return Create(
            customerId,
            title,
            TimeRange.Create(startsAt, endsAt),
            notes);
    }

    public static Appointment Restore(
        Guid id,
        Guid customerId,
        string title,
        DateTime startsAt,
        DateTime endsAt,
        string? notes,
        bool isCancelled)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Appointment id is required.", nameof(id));
        }

        var appointment = new Appointment(
            id,
            customerId,
            title,
            TimeRange.Create(startsAt, endsAt),
            notes);

        if (isCancelled)
        {
            appointment.Cancel();
        }

        return appointment;
    }

    public void ChangeTitle(string title)
    {
        Title = TextNormalizer.NormalizeRequired(title, nameof(title), "Title is required.");
    }

    public void ChangeNotes(string? notes)
    {
        Notes = TextNormalizer.NormalizeOptional(notes);
    }

    public void Reschedule(TimeRange newTime)
    {
        ArgumentNullException.ThrowIfNull(newTime);

        Time = newTime;
    }

    public void Reschedule(DateTime startsAt, DateTime endsAt)
    {
        Reschedule(TimeRange.Create(startsAt, endsAt));
    }

    public void Cancel()
    {
        IsCancelled = true;
    }

    public bool OverlapsWith(TimeRange other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Time.OverlapsWith(other);
    }

    public bool OverlapsWith(DateTime startsAt, DateTime endsAt)
    {
        return OverlapsWith(TimeRange.Create(startsAt, endsAt));
    }
}