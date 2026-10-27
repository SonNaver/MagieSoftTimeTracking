using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Tests.Model;

public sealed class TimeEntryTests
{
	private static readonly DateTimeOffset Start = new(2026, 9, 24, 14, 0, 0, TimeSpan.FromHours(2));

	[Fact]
	public void StartEntry_NewEntry_IsOpenAndHasNoEnd()
	{
		TimeEntry entry = TimeEntry.StartEntry(Start, "Mathematik II", "Uebungsblatt 3");

		Assert.True(entry.IsOpen);
		Assert.Null(entry.End);
		Assert.Null(entry.Duration);
	}

	[Fact]
	public void StartEntry_NewEntry_AssignsDistinctIds()
	{
		TimeEntry first = TimeEntry.StartEntry(Start, "Mathematik II", null);
		TimeEntry second = TimeEntry.StartEntry(Start, "Mathematik II", null);

		Assert.NotEqual(Guid.Empty, first.Id);
		Assert.NotEqual(first.Id, second.Id);
	}

	[Fact]
	public void StartEntry_NullNote_BecomesEmptyText()
	{
		TimeEntry entry = TimeEntry.StartEntry(Start, "Mathematik II", null);

		Assert.Equal(string.Empty, entry.Note);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void StartEntry_BlankModule_ThrowsArgumentException(string module)
	{
		Assert.Throws<ArgumentException>(() => TimeEntry.StartEntry(Start, module, null));
	}

	[Fact]
	public void CompleteEntry_OpenEntry_SetsEndAndClosesIt()
	{
		TimeEntry entry = TimeEntry.StartEntry(Start, "Mathematik II", null);
		DateTimeOffset end = Start.AddMinutes(90);

		entry.CompleteEntry(end);

		Assert.False(entry.IsOpen);
		Assert.Equal(end, entry.End);
	}

	[Fact]
	public void Duration_CompletedEntry_IsSpanBetweenStartAndEnd()
	{
		TimeEntry entry = TimeEntry.StartEntry(Start, "Mathematik II", null);

		entry.CompleteEntry(Start.AddMinutes(90));

		Assert.Equal(TimeSpan.FromMinutes(90), entry.Duration);
	}

	[Fact]
	public void Duration_EntrySpanningDaylightSavingEnd_CountsElapsedTimeNotWallClock()
	{
		// In the night of 25 October 2026 the clock is put back from 03:00 CEST to 02:00 CET.
		// The wall clock shows only 30 minutes between these two points in time, but 90
		// minutes have passed — exactly the case that the time zone offset in the
		// stored timestamps makes unambiguous.
		DateTimeOffset beforeSwitch = new(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(2));
		DateTimeOffset afterSwitch = new(2026, 10, 25, 3, 0, 0, TimeSpan.FromHours(1));
		TimeEntry entry = TimeEntry.StartEntry(beforeSwitch, "Mathematik II", null);

		entry.CompleteEntry(afterSwitch);

		Assert.Equal(TimeSpan.FromMinutes(90), entry.Duration);
		Assert.NotEqual(TimeSpan.FromMinutes(30), entry.Duration);
	}

	[Fact]
	public void CompleteEntry_EndEqualsStart_IsAllowedAndYieldsZeroDuration()
	{
		TimeEntry entry = TimeEntry.StartEntry(Start, "Mathematik II", null);

		entry.CompleteEntry(Start);

		Assert.Equal(TimeSpan.Zero, entry.Duration);
	}

	[Fact]
	public void CompleteEntry_EndBeforeStart_ThrowsArgumentOutOfRangeException()
	{
		TimeEntry entry = TimeEntry.StartEntry(Start, "Mathematik II", null);

		Assert.Throws<ArgumentOutOfRangeException>(() => entry.CompleteEntry(Start.AddMinutes(-1)));
	}

	[Fact]
	public void CompleteEntry_AlreadyCompletedEntry_ThrowsInvalidOperationException()
	{
		TimeEntry entry = TimeEntry.StartEntry(Start, "Mathematik II", null);
		entry.CompleteEntry(Start.AddMinutes(90));

		Assert.Throws<InvalidOperationException>(() => entry.CompleteEntry(Start.AddMinutes(120)));
	}

	[Fact]
	public void RestoreEntry_StoredCompletedEntry_KeepsIdAndTimes()
	{
		Guid id = Guid.NewGuid();
		DateTimeOffset end = Start.AddMinutes(45);

		TimeEntry entry = TimeEntry.RestoreEntry(id, Start, end, "Mathematik II", "Uebungsblatt 3");

		Assert.Equal(id, entry.Id);
		Assert.Equal(Start, entry.Start);
		Assert.Equal(end, entry.End);
		Assert.Equal(TimeSpan.FromMinutes(45), entry.Duration);
	}

	[Fact]
	public void RestoreEntry_StoredEntryWithoutEnd_IsOpen()
	{
		TimeEntry entry = TimeEntry.RestoreEntry(Guid.NewGuid(), Start, null, "Mathematik II", null);

		Assert.True(entry.IsOpen);
	}

	[Fact]
	public void RestoreEntry_EndBeforeStart_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TimeEntry.RestoreEntry(Guid.NewGuid(), Start, Start.AddMinutes(-1), "Mathematik II", null));
	}

	[Fact]
	public void RestoreEntry_MissingId_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(
			() => TimeEntry.RestoreEntry(Guid.Empty, Start, null, "Mathematik II", null));
	}

	[Fact]
	public void RestoreEntry_BlankModule_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(
			() => TimeEntry.RestoreEntry(Guid.NewGuid(), Start, null, "   ", null));
	}
}
