using System;

namespace BusinessJournal.Domain.ValueObjects;

public sealed record TimeRange
{
    public DateTime Start { get; }
    public DateTime End { get; }

    private TimeRange(DateTime start, DateTime end)
    {
        if (end <= start)
        {
            throw new ArgumentException("End time must be later than start time.");
        }

        Start = start;
        End = end;
    }

    public static TimeRange Create(DateTime start, DateTime end)
    {
        return new TimeRange(start, end);
    }

    public bool OverlapsWith(TimeRange other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Start < other.End && End > other.Start;
    }
}