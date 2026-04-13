using BusinessJournal.Domain.ValueObjects;
using Xunit;

namespace BusinessJournal.Tests.Domain;

public sealed class TimeRangeTests
{
    [Fact]
    public void Create_WithValidValues_ShouldCreateTimeRange()
    {
        var start = new DateTime(2026, 4, 9, 10, 0, 0);
        var end = new DateTime(2026, 4, 9, 11, 0, 0);

        var timeRange = TimeRange.Create(start, end);

        Assert.Equal(start, timeRange.Start);
        Assert.Equal(end, timeRange.End);
    }

    [Fact]
    public void Create_WhenEndIsEarlierThanStart_ShouldThrowArgumentException()
    {
        var start = new DateTime(2026, 4, 9, 11, 0, 0);
        var end = new DateTime(2026, 4, 9, 10, 0, 0);

        Assert.Throws<ArgumentException>(() =>
            TimeRange.Create(start, end));
    }

    [Fact]
    public void Create_WhenEndEqualsStart_ShouldThrowArgumentException()
    {
        var start = new DateTime(2026, 4, 9, 10, 0, 0);
        var end = new DateTime(2026, 4, 9, 10, 0, 0);

        Assert.Throws<ArgumentException>(() =>
            TimeRange.Create(start, end));
    }

    [Fact]
    public void OverlapsWith_WhenRangesOverlap_ShouldReturnTrue()
    {
        var firstRange = TimeRange.Create(
            new DateTime(2026, 4, 9, 10, 0, 0),
            new DateTime(2026, 4, 9, 11, 0, 0));

        var secondRange = TimeRange.Create(
            new DateTime(2026, 4, 9, 10, 30, 0),
            new DateTime(2026, 4, 9, 11, 30, 0));

        var result = firstRange.OverlapsWith(secondRange);

        Assert.True(result);
    }

    [Fact]
    public void OverlapsWith_WhenRangesDoNotOverlap_ShouldReturnFalse()
    {
        var firstRange = TimeRange.Create(
            new DateTime(2026, 4, 9, 10, 0, 0),
            new DateTime(2026, 4, 9, 11, 0, 0));

        var secondRange = TimeRange.Create(
            new DateTime(2026, 4, 9, 11, 0, 0),
            new DateTime(2026, 4, 9, 12, 0, 0));

        var result = firstRange.OverlapsWith(secondRange);

        Assert.False(result);
    }
}