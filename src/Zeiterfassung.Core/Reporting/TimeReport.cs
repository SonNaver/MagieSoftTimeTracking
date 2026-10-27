using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Reporting;

/// <summary>
/// Gathers recorded time entries into totals.
/// </summary>
/// <remarks>
/// All the arithmetic of the report lives here and thereby in the tested core; the
/// user interface only displays the results. The <em>open</em> entry enters no total — it has
/// no duration yet — but it does appear in the entry list.
/// </remarks>
public static class TimeReport
{
	/// <summary>
	/// Gathers the entries of a period by module.
	/// </summary>
	/// <remarks>
	/// Sorted by time spent, the largest on top; by module name when equal. Grouping ignores
	/// case.
	/// </remarks>
	/// <param name="entries">The entries to report on.</param>
	/// <param name="range">The period to narrow down to.</param>
	/// <param name="knownModules">The maintained module list, or null when it is unavailable.</param>
	/// <param name="moduleFilter">The modules to narrow down to, or null for all.</param>
	/// <returns>One total per module, descending by time.</returns>
	public static IReadOnlyList<ModuleTotal> SummarizeByModule(
		IEnumerable<TimeEntry> entries,
		DateRange range,
		StudyModuleList? knownModules,
		ModuleFilter? moduleFilter = null)
	{
		ArgumentNullException.ThrowIfNull(entries);
		ArgumentNullException.ThrowIfNull(range);

		Dictionary<string, string> knownSpellings = CollectKnownSpellings(knownModules);

		return SelectCompletedEntries(entries, range, moduleFilter)
			.GroupBy(entry => entry.Module, StringComparer.OrdinalIgnoreCase)
			.Select(group => CreateModuleTotal(group, knownSpellings))
			.OrderByDescending(total => total.Total)
			.ThenBy(total => total.Module, StringComparer.CurrentCultureIgnoreCase)
			.ToList();
	}

	/// <summary>
	/// Gathers the entries of a period by calendar day.
	/// </summary>
	/// <remarks>
	/// Days without entries between the first and the last occupied day appear with a total of
	/// zero, so that a course of work shows a break as a break.
	/// </remarks>
	/// <param name="entries">The entries to report on.</param>
	/// <param name="range">The period to narrow down to.</param>
	/// <param name="moduleFilter">The modules to narrow down to, or null for all.</param>
	/// <returns>One total per day, chronological and without gaps.</returns>
	public static IReadOnlyList<PeriodTotal> SummarizeByDay(
		IEnumerable<TimeEntry> entries,
		DateRange range,
		ModuleFilter? moduleFilter = null)
	{
		return SummarizePeriods(entries, range, moduleFilter, GetDayStart, day => day.AddDays(1));
	}

	/// <summary>
	/// Gathers the entries of a period by calendar week.
	/// </summary>
	/// <remarks>
	/// Counted according to ISO 8601, so the week begins on Monday. Weeks without entries
	/// between the first and the last occupied week appear with a total of zero.
	/// </remarks>
	/// <param name="entries">The entries to report on.</param>
	/// <param name="range">The period to narrow down to.</param>
	/// <param name="moduleFilter">The modules to narrow down to, or null for all.</param>
	/// <returns>One total per week, chronological and without gaps, dated to the Monday.</returns>
	public static IReadOnlyList<PeriodTotal> SummarizeByWeek(
		IEnumerable<TimeEntry> entries,
		DateRange range,
		ModuleFilter? moduleFilter = null)
	{
		return SummarizePeriods(entries, range, moduleFilter, GetWeekStart, week => week.AddDays(7));
	}

	/// <summary>
	/// Computes the average total per period.
	/// </summary>
	/// <remarks>
	/// Every period of the sequence counts, the empty ones in between included — an average
	/// over only the busy days would say how long a working day is, not how long a day is.
	/// </remarks>
	/// <param name="totals">The period totals, as returned by the daily or weekly report.</param>
	/// <returns>The mean of the totals, or zero for an empty sequence.</returns>
	public static TimeSpan ComputeAverage(IReadOnlyList<PeriodTotal> totals)
	{
		ArgumentNullException.ThrowIfNull(totals);

		if (totals.Count == 0)
		{
			return TimeSpan.Zero;
		}

		return SumDurations(totals.Select(total => total.Total)) / totals.Count;
	}

	/// <summary>
	/// Selects the entries of a period for the list, the most recent first.
	/// </summary>
	/// <remarks>
	/// Unlike the totals, this list holds the open entry as well: whoever looks up what they
	/// did last wants to see the running session.
	/// </remarks>
	/// <param name="entries">The entries to report on.</param>
	/// <param name="range">The period to narrow down to.</param>
	/// <param name="moduleFilter">The modules to narrow down to, or null for all.</param>
	/// <returns>The entries of the period, descending by start.</returns>
	public static IReadOnlyList<TimeEntry> SelectRecentEntries(
		IEnumerable<TimeEntry> entries,
		DateRange range,
		ModuleFilter? moduleFilter = null)
	{
		ArgumentNullException.ThrowIfNull(entries);
		ArgumentNullException.ThrowIfNull(range);

		ModuleFilter filter = moduleFilter ?? ModuleFilter.All;

		return entries
			.Where(entry => range.ContainsEntry(entry) && filter.Matches(entry))
			.OrderByDescending(entry => entry.Start)
			.ToList();
	}

	/// <summary>
	/// Determines the Monday of the week a day lies in.
	/// </summary>
	/// <remarks>
	/// The Monday suffices as the mark of a week according to ISO 8601: because the week begins
	/// on Monday there, exactly the days of one ISO week belong to the same Monday — across the
	/// turn of the year as well.
	/// </remarks>
	/// <param name="day">The day whose week is wanted.</param>
	/// <returns>The Monday of that week.</returns>
	public static DateOnly GetWeekStart(DateOnly day)
	{
		int daysSinceMonday = ((int)day.DayOfWeek + 6) % 7;

		return day.AddDays(-daysSinceMonday);
	}

	/// <summary>
	/// Returns a day unchanged as the start of its period.
	/// </summary>
	/// <param name="day">The day.</param>
	/// <returns>The same day.</returns>
	private static DateOnly GetDayStart(DateOnly day)
	{
		return day;
	}

	/// <summary>
	/// Gathers the entries into a gapless sequence of periods.
	/// </summary>
	/// <param name="entries">The entries to report on.</param>
	/// <param name="range">The period to narrow down to.</param>
	/// <param name="moduleFilter">The modules to narrow down to, or null for all.</param>
	/// <param name="findPeriodStart">Determines the start of the period a day belongs to.</param>
	/// <param name="findNextPeriod">Determines the period start following a given one.</param>
	/// <returns>One total per period, chronological and without gaps.</returns>
	private static IReadOnlyList<PeriodTotal> SummarizePeriods(
		IEnumerable<TimeEntry> entries,
		DateRange range,
		ModuleFilter? moduleFilter,
		Func<DateOnly, DateOnly> findPeriodStart,
		Func<DateOnly, DateOnly> findNextPeriod)
	{
		ArgumentNullException.ThrowIfNull(entries);
		ArgumentNullException.ThrowIfNull(range);

		Dictionary<DateOnly, PeriodTotal> totals = SelectCompletedEntries(entries, range, moduleFilter)
			.GroupBy(entry => findPeriodStart(DateOnly.FromDateTime(entry.Start.Date)))
			.ToDictionary(
				group => group.Key,
				group => CreatePeriodTotal(group.Key, group));

		if (totals.Count == 0)
		{
			return [];
		}

		return FillGaps(totals, findNextPeriod);
	}

	/// <summary>
	/// Lines the periods up chronologically and adds the empty ones in between.
	/// </summary>
	/// <remarks>
	/// The filling happens only between the first and the last occupied period, not out to the
	/// bounds of the chosen range. A widely drawn range would otherwise produce hundreds of
	/// empty days that tell nobody anything.
	/// </remarks>
	/// <param name="totals">The occupied periods, keyed by their start.</param>
	/// <param name="findNextPeriod">Determines the period start following a given one.</param>
	/// <returns>The gapless sequence of periods.</returns>
	private static IReadOnlyList<PeriodTotal> FillGaps(
		Dictionary<DateOnly, PeriodTotal> totals,
		Func<DateOnly, DateOnly> findNextPeriod)
	{
		DateOnly firstPeriod = totals.Keys.Min();
		DateOnly lastPeriod = totals.Keys.Max();

		List<PeriodTotal> result = [];

		for (DateOnly period = firstPeriod; period <= lastPeriod; period = findNextPeriod(period))
		{
			result.Add(totals.TryGetValue(period, out PeriodTotal? total)
				? total
				: new PeriodTotal(period, TimeSpan.Zero, 0));
		}

		return result;
	}

	/// <summary>
	/// Forms the total of one period from its entries, broken down by module.
	/// </summary>
	/// <param name="periodStart">The first day of the period.</param>
	/// <param name="group">The completed entries of the period.</param>
	/// <returns>The total of the period.</returns>
	private static PeriodTotal CreatePeriodTotal(DateOnly periodStart, IEnumerable<TimeEntry> group)
	{
		List<TimeEntry> entries = group.ToList();

		Dictionary<string, TimeSpan> moduleTimes = entries
			.GroupBy(entry => entry.Module, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(
				moduleGroup => moduleGroup.Key,
				moduleGroup => SumDurations(moduleGroup.Select(entry => entry.Duration!.Value)),
				StringComparer.OrdinalIgnoreCase);

		return new PeriodTotal(periodStart, SumDurations(entries), entries.Count, moduleTimes);
	}

	/// <summary>
	/// Selects the completed entries of the period.
	/// </summary>
	/// <param name="entries">The entries to report on.</param>
	/// <param name="range">The period to narrow down to.</param>
	/// <param name="moduleFilter">The modules to narrow down to, or null for all.</param>
	/// <returns>The completed entries that begin within the period and pass the filter.</returns>
	private static IEnumerable<TimeEntry> SelectCompletedEntries(
		IEnumerable<TimeEntry> entries,
		DateRange range,
		ModuleFilter? moduleFilter)
	{
		ModuleFilter filter = moduleFilter ?? ModuleFilter.All;

		return entries.Where(entry => !entry.IsOpen && range.ContainsEntry(entry) && filter.Matches(entry));
	}

	/// <summary>
	/// Forms the module total from a group of entries sharing a name.
	/// </summary>
	/// <param name="group">The entries of one module.</param>
	/// <param name="knownSpellings">The spellings of the maintained module list.</param>
	/// <returns>The total of that module.</returns>
	private static ModuleTotal CreateModuleTotal(
		IGrouping<string, TimeEntry> group,
		Dictionary<string, string> knownSpellings)
	{
		bool isKnownModule = knownSpellings.TryGetValue(group.Key, out string? knownSpelling);

		string module = isKnownModule
			? knownSpelling!
			: group.OrderByDescending(entry => entry.Start).First().Module;

		return new ModuleTotal(module, SumDurations(group), group.Count(), isKnownModule);
	}

	/// <summary>
	/// Puts the spellings of the maintained module list aside for lookup.
	/// </summary>
	/// <param name="knownModules">The maintained module list, or null when it is unavailable.</param>
	/// <returns>The module names, lookable up ignoring case.</returns>
	private static Dictionary<string, string> CollectKnownSpellings(StudyModuleList? knownModules)
	{
		Dictionary<string, string> spellings = new(StringComparer.OrdinalIgnoreCase);

		if (knownModules is null)
		{
			return spellings;
		}

		foreach (StudyModule module in knownModules.Modules)
		{
			spellings[module.Name] = module.Name;
		}

		return spellings;
	}

	/// <summary>
	/// Sums the durations of completed entries.
	/// </summary>
	/// <param name="entries">The completed entries.</param>
	/// <returns>The total duration.</returns>
	private static TimeSpan SumDurations(IEnumerable<TimeEntry> entries)
	{
		return SumDurations(entries.Select(entry => entry.Duration!.Value));
	}

	/// <summary>
	/// Sums a sequence of durations.
	/// </summary>
	/// <param name="durations">The durations.</param>
	/// <returns>The total duration.</returns>
	private static TimeSpan SumDurations(IEnumerable<TimeSpan> durations)
	{
		TimeSpan total = TimeSpan.Zero;

		foreach (TimeSpan duration in durations)
		{
			total += duration;
		}

		return total;
	}
}
