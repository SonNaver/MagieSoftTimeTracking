using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Reporting;

namespace Zeiterfassung.Core.Tests.Reporting;

public sealed class TimeReportTests
{
	private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

	[Fact]
	public void SummarizeByModule_SeveralEntriesPerModule_AddsThemUp()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-09-22 08:00", "2026-09-22 09:30", "Mathematik II"),
			CreateEntry("2026-09-22 14:00", "2026-09-22 15:00", "Statistik"),
		];

		IReadOnlyList<ModuleTotal> totals = TimeReport.SummarizeByModule(entries, DateRange.Everything, null);

		Assert.Equal(2, totals.Count);
		Assert.Equal("Mathematik II", totals[0].Module);
		Assert.Equal(TimeSpan.FromHours(3.5), totals[0].Total);
		Assert.Equal(2, totals[0].EntryCount);
		Assert.Equal("Statistik", totals[1].Module);
		Assert.Equal(TimeSpan.FromHours(1), totals[1].Total);
	}

	[Fact]
	public void SummarizeByModule_ModulesWithDifferentTotals_PutsTheLargestFirst()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik"),
			CreateEntry("2026-09-21 10:00", "2026-09-21 14:00", "Betriebssysteme"),
			CreateEntry("2026-09-21 15:00", "2026-09-21 17:00", "Mathematik II"),
		];

		IReadOnlyList<ModuleTotal> totals = TimeReport.SummarizeByModule(entries, DateRange.Everything, null);

		Assert.Equal(["Betriebssysteme", "Mathematik II", "Statistik"], totals.Select(total => total.Module));
	}

	[Fact]
	public void SummarizeByModule_OpenEntry_IsNotCounted()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateOpenEntry("2026-09-21 11:00", "Mathematik II"),
		];

		IReadOnlyList<ModuleTotal> totals = TimeReport.SummarizeByModule(entries, DateRange.Everything, null);

		Assert.Equal(TimeSpan.FromHours(2), totals[0].Total);
		Assert.Equal(1, totals[0].EntryCount);
	}

	[Fact]
	public void SummarizeByModule_SameNameInDifferentCasing_BecomesOneModule()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-09-22 08:00", "2026-09-22 09:00", "mathematik ii"),
		];

		IReadOnlyList<ModuleTotal> totals = TimeReport.SummarizeByModule(entries, DateRange.Everything, null);

		Assert.Single(totals);
		Assert.Equal(TimeSpan.FromHours(3), totals[0].Total);
	}

	[Fact]
	public void SummarizeByModule_ModuleStillInTheList_UsesTheSpellingFromTheList()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "mathematik ii")];
		StudyModuleList modules = new([new StudyModule("Mathematik II")]);

		IReadOnlyList<ModuleTotal> totals = TimeReport.SummarizeByModule(entries, DateRange.Everything, modules);

		Assert.Equal("Mathematik II", totals[0].Module);
		Assert.True(totals[0].IsKnownModule);
	}

	[Fact]
	public void SummarizeByModule_ModuleMissingFromTheList_IsMarkedAsUnknown()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Altes Modul")];
		StudyModuleList modules = new([new StudyModule("Mathematik II")]);

		IReadOnlyList<ModuleTotal> totals = TimeReport.SummarizeByModule(entries, DateRange.Everything, modules);

		Assert.Equal("Altes Modul", totals[0].Module);
		Assert.False(totals[0].IsKnownModule);
		Assert.Equal(TimeSpan.FromHours(2), totals[0].Total);
	}

	[Fact]
	public void SummarizeByModule_WithoutAModuleList_MarksEveryModuleAsUnknown()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II")];

		IReadOnlyList<ModuleTotal> totals = TimeReport.SummarizeByModule(entries, DateRange.Everything, null);

		Assert.False(totals[0].IsKnownModule);
	}

	[Fact]
	public void SummarizeByModule_EntriesOutsideTheRange_AreLeftOut()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-19 08:00", "2026-09-19 10:00", "Mathematik II"),
			CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Mathematik II"),
		];
		DateRange range = DateRange.CreateRange(new DateOnly(2026, 9, 20), null);

		IReadOnlyList<ModuleTotal> totals = TimeReport.SummarizeByModule(entries, range, null);

		Assert.Equal(TimeSpan.FromHours(1), totals[0].Total);
	}

	[Fact]
	public void SummarizeByModule_NoEntriesAtAll_ReturnsAnEmptyList()
	{
		IReadOnlyList<ModuleTotal> totals = TimeReport.SummarizeByModule([], DateRange.Everything, null);

		Assert.Empty(totals);
	}

	[Fact]
	public void SummarizeByDay_EntryRunningPastMidnight_CountsTowardsItsStartDay()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 23:30", "2026-09-22 00:30", "Mathematik II")];

		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByDay(entries, DateRange.Everything);

		Assert.Single(totals);
		Assert.Equal(new DateOnly(2026, 9, 21), totals[0].PeriodStart);
		Assert.Equal(TimeSpan.FromHours(1), totals[0].Total);
	}

	[Fact]
	public void SummarizeByDay_DaysWithoutEntriesInBetween_AppearWithZero()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-09-24 08:00", "2026-09-24 09:00", "Mathematik II"),
		];

		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByDay(entries, DateRange.Everything);

		Assert.Equal(4, totals.Count);
		Assert.Equal(
			[new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 23), new DateOnly(2026, 9, 24)],
			totals.Select(total => total.PeriodStart));
		Assert.True(totals[1].IsEmpty);
		Assert.True(totals[2].IsEmpty);
		Assert.Equal(TimeSpan.Zero, totals[1].Total);
	}

	[Fact]
	public void SummarizeByDay_NoEntriesAtAll_ReturnsAnEmptyListInsteadOfZeroRows()
	{
		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByDay([], DateRange.Everything);

		Assert.Empty(totals);
	}

	[Fact]
	public void SummarizeByWeek_EntriesFromMondayToSunday_BecomeOneWeek()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-09-27 08:00", "2026-09-27 09:00", "Statistik"),
		];

		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByWeek(entries, DateRange.Everything);

		Assert.Single(totals);
		Assert.Equal(new DateOnly(2026, 9, 21), totals[0].PeriodStart);
		Assert.Equal(TimeSpan.FromHours(3), totals[0].Total);
		Assert.Equal(2, totals[0].EntryCount);
	}

	[Fact]
	public void SummarizeByWeek_WeekSpanningTheTurnOfTheYear_StaysOneWeek()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-12-31 08:00", "2026-12-31 10:00", "Mathematik II"),
			CreateEntry("2027-01-02 08:00", "2027-01-02 09:00", "Mathematik II"),
		];

		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByWeek(entries, DateRange.Everything);

		Assert.Single(totals);
		Assert.Equal(new DateOnly(2026, 12, 28), totals[0].PeriodStart);
		Assert.Equal(TimeSpan.FromHours(3), totals[0].Total);
	}

	[Fact]
	public void SummarizeByWeek_WeeksWithoutEntriesInBetween_AppearWithZero()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-10-12 08:00", "2026-10-12 09:00", "Mathematik II"),
		];

		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByWeek(entries, DateRange.Everything);

		Assert.Equal(4, totals.Count);
		Assert.Equal(new DateOnly(2026, 9, 28), totals[1].PeriodStart);
		Assert.True(totals[1].IsEmpty);
		Assert.True(totals[2].IsEmpty);
		Assert.False(totals[3].IsEmpty);
	}

	[Theory]
	[InlineData("2026-09-21", "2026-09-21")]
	[InlineData("2026-09-24", "2026-09-21")]
	[InlineData("2026-09-27", "2026-09-21")]
	[InlineData("2027-01-01", "2026-12-28")]
	public void GetWeekStart_AnyDayOfTheWeek_ReturnsItsMonday(string day, string expectedMonday)
	{
		DateOnly weekStart = TimeReport.GetWeekStart(DateOnly.Parse(day));

		Assert.Equal(DateOnly.Parse(expectedMonday), weekStart);
	}

	[Fact]
	public void SelectRecentEntries_SeveralEntries_PutsTheNewestFirst()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-09-24 08:00", "2026-09-24 09:00", "Statistik"),
		];

		IReadOnlyList<TimeEntry> recent = TimeReport.SelectRecentEntries(entries, DateRange.Everything);

		Assert.Equal(["Statistik", "Mathematik II"], recent.Select(entry => entry.Module));
	}

	[Fact]
	public void SelectRecentEntries_OpenEntry_IsIncludedUnlikeInTheSums()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateOpenEntry("2026-09-24 08:00", "Statistik"),
		];

		IReadOnlyList<TimeEntry> recent = TimeReport.SelectRecentEntries(entries, DateRange.Everything);

		Assert.Equal(2, recent.Count);
		Assert.True(recent[0].IsOpen);
	}

	[Fact]
	public void SummarizeByDay_WithAModuleFilter_CountsOnlyTheSelectedModules()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-09-21 11:00", "2026-09-21 12:00", "Statistik"),
			CreateEntry("2026-09-22 08:00", "2026-09-22 09:00", "Betriebssysteme"),
		];

		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByDay(
			entries,
			DateRange.Everything,
			new ModuleFilter(["mathematik ii", "Betriebssysteme"]));

		Assert.Equal(2, totals.Count);
		Assert.Equal(TimeSpan.FromHours(2), totals[0].Total);
		Assert.Equal(TimeSpan.FromHours(1), totals[1].Total);
	}

	[Fact]
	public void SummarizeByWeek_ModuleFilterWithoutSelection_LetsEverythingPass()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-09-22 08:00", "2026-09-22 09:00", "Statistik"),
		];

		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByWeek(entries, DateRange.Everything, new ModuleFilter([]));

		Assert.Equal(TimeSpan.FromHours(3), totals[0].Total);
	}

	[Fact]
	public void SummarizeByWeek_SeveralModules_BreaksTheWeekDownByModule()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-09-22 08:00", "2026-09-22 09:00", "mathematik ii"),
			CreateEntry("2026-09-23 08:00", "2026-09-23 09:30", "Statistik"),
		];

		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByWeek(entries, DateRange.Everything);

		Assert.Equal(TimeSpan.FromHours(3), totals[0].ModuleTimes["Mathematik II"]);
		Assert.Equal(TimeSpan.FromHours(1.5), totals[0].ModuleTimes["Statistik"]);
		Assert.Equal(totals[0].Total, totals[0].ModuleTimes.Values.Aggregate(TimeSpan.Zero, (sum, time) => sum + time));
	}

	[Fact]
	public void SummarizeByDay_ModuleWithoutTimeOnADay_IsNotInThatDaysBreakdown()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-09-22 08:00", "2026-09-22 09:00", "Statistik"),
		];

		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByDay(entries, DateRange.Everything);

		Assert.False(totals[0].ModuleTimes.ContainsKey("Statistik"));
	}

	[Fact]
	public void ComputeAverage_DaysWithAGapInBetween_CountsTheEmptyDayAsZero()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateEntry("2026-09-23 08:00", "2026-09-23 12:00", "Mathematik II"),
		];

		IReadOnlyList<PeriodTotal> totals = TimeReport.SummarizeByDay(entries, DateRange.Everything);

		Assert.Equal(TimeSpan.FromHours(2), TimeReport.ComputeAverage(totals));
	}

	[Fact]
	public void ComputeAverage_NoPeriods_ReturnsZero()
	{
		Assert.Equal(TimeSpan.Zero, TimeReport.ComputeAverage([]));
	}

	[Fact]
	public void SelectRecentEntries_WithAModuleFilter_KeepsOnlyTheSelectedModules()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 10:00", "Mathematik II"),
			CreateOpenEntry("2026-09-24 08:00", "Statistik"),
		];

		IReadOnlyList<TimeEntry> recent = TimeReport.SelectRecentEntries(
			entries,
			DateRange.Everything,
			new ModuleFilter(["Statistik"]));

		Assert.Single(recent);
		Assert.True(recent[0].IsOpen);
	}

	/// <summary>
	/// Creates a completed entry from two time values.
	/// </summary>
	/// <param name="start">The start in the pattern „yyyy-mm-dd hh:mm“.</param>
	/// <param name="end">The end in the same pattern.</param>
	/// <param name="module">The module name.</param>
	/// <returns>The finished entry.</returns>
	private static TimeEntry CreateEntry(string start, string end, string module)
	{
		return TimeEntry.RestoreEntry(Guid.NewGuid(), ParseMoment(start), ParseMoment(end), module, null);
	}

	/// <summary>
	/// Creates an open entry without an end.
	/// </summary>
	/// <param name="start">The start in the pattern „yyyy-mm-dd hh:mm“.</param>
	/// <param name="module">The module name.</param>
	/// <returns>The open entry.</returns>
	private static TimeEntry CreateOpenEntry(string start, string module)
	{
		return TimeEntry.RestoreEntry(Guid.NewGuid(), ParseMoment(start), null, module, null);
	}

	/// <summary>
	/// Turns a time value of the tests into a point in time with a fixed offset.
	/// </summary>
	/// <remarks>
	/// The offset is deliberately fixed and not that of the running machine, so that the tests
	/// deliver the same result regardless of the machine's time zone.
	/// </remarks>
	/// <param name="moment">The time value in the pattern „yyyy-mm-dd hh:mm“.</param>
	/// <returns>The point in time as local time with an offset.</returns>
	private static DateTimeOffset ParseMoment(string moment)
	{
		DateTime local = DateTime.ParseExact(moment, "yyyy-MM-dd HH:mm", null);

		return new DateTimeOffset(local, Offset);
	}
}
