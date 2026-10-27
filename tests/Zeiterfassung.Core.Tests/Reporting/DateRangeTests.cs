using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Reporting;

namespace Zeiterfassung.Core.Tests.Reporting;

public sealed class DateRangeTests
{
	private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

	[Fact]
	public void CreateRange_LastDayBeforeFirstDay_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => DateRange.CreateRange(new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 24)));
	}

	[Fact]
	public void CreateRange_SameDayAsBothBounds_IsAllowed()
	{
		DateRange range = DateRange.CreateRange(new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 25));

		Assert.True(range.ContainsEntry(CreateEntry(new DateTimeOffset(2026, 9, 25, 8, 0, 0, Offset))));
	}

	[Fact]
	public void Everything_WithoutAnyBound_IsUnbounded()
	{
		Assert.True(DateRange.Everything.IsUnbounded);
	}

	[Fact]
	public void Everything_EntryFromAnyDay_IsIncluded()
	{
		Assert.True(DateRange.Everything.ContainsEntry(
			CreateEntry(new DateTimeOffset(1999, 1, 1, 8, 0, 0, Offset))));
	}

	[Fact]
	public void ContainsEntry_EntryOnTheFirstDay_IsIncluded()
	{
		DateRange range = DateRange.CreateRange(new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 25));

		Assert.True(range.ContainsEntry(CreateEntry(new DateTimeOffset(2026, 9, 20, 0, 0, 0, Offset))));
	}

	[Fact]
	public void ContainsEntry_EntryOnTheLastDay_IsIncluded()
	{
		DateRange range = DateRange.CreateRange(new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 25));

		Assert.True(range.ContainsEntry(CreateEntry(new DateTimeOffset(2026, 9, 25, 23, 59, 59, Offset))));
	}

	[Fact]
	public void ContainsEntry_EntryBeforeTheRange_IsExcluded()
	{
		DateRange range = DateRange.CreateRange(new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 25));

		Assert.False(range.ContainsEntry(CreateEntry(new DateTimeOffset(2026, 9, 19, 23, 59, 59, Offset))));
	}

	[Fact]
	public void ContainsEntry_EntryAfterTheRange_IsExcluded()
	{
		DateRange range = DateRange.CreateRange(new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 25));

		Assert.False(range.ContainsEntry(CreateEntry(new DateTimeOffset(2026, 9, 26, 0, 0, 0, Offset))));
	}

	[Fact]
	public void ContainsEntry_EntryStartingInTheRangeAndEndingAfterIt_IsIncluded()
	{
		DateRange range = DateRange.CreateRange(new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 25));
		TimeEntry entry = TimeEntry.RestoreEntry(
			Guid.NewGuid(),
			new DateTimeOffset(2026, 9, 25, 23, 50, 0, Offset),
			new DateTimeOffset(2026, 9, 26, 0, 30, 0, Offset),
			"Mathematik II",
			null);

		Assert.True(range.ContainsEntry(entry));
	}

	[Fact]
	public void ContainsEntry_OnlyLastDayGiven_IncludesEverythingBefore()
	{
		DateRange range = DateRange.CreateRange(null, new DateOnly(2026, 9, 25));

		Assert.True(range.ContainsEntry(CreateEntry(new DateTimeOffset(1999, 1, 1, 8, 0, 0, Offset))));
		Assert.False(range.ContainsEntry(CreateEntry(new DateTimeOffset(2026, 9, 26, 8, 0, 0, Offset))));
	}

	[Fact]
	public void ContainsEntry_OnlyFirstDayGiven_IncludesEverythingAfter()
	{
		DateRange range = DateRange.CreateRange(new DateOnly(2026, 9, 25), null);

		Assert.False(range.ContainsEntry(CreateEntry(new DateTimeOffset(2026, 9, 24, 8, 0, 0, Offset))));
		Assert.True(range.ContainsEntry(CreateEntry(new DateTimeOffset(2099, 1, 1, 8, 0, 0, Offset))));
	}

	/// <summary>
	/// Creates a completed entry lasting one hour.
	/// </summary>
	/// <param name="start">The start of the entry.</param>
	/// <returns>The finished entry.</returns>
	private static TimeEntry CreateEntry(DateTimeOffset start)
	{
		return TimeEntry.RestoreEntry(Guid.NewGuid(), start, start.AddHours(1), "Mathematik II", null);
	}
}
