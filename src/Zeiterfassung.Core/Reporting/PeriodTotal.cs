namespace Zeiterfassung.Core.Reporting;

/// <summary>
/// The total time spent in one period — a day or a week.
/// </summary>
public sealed class PeriodTotal
{
	/// <summary>
	/// Creates a period total.
	/// </summary>
	/// <param name="periodStart">The first day of the period.</param>
	/// <param name="total">The summed time of all completed entries.</param>
	/// <param name="entryCount">The number of entries the sum comes from.</param>
	/// <param name="moduleTimes">The summed time per module, or null when not broken down.</param>
	public PeriodTotal(
		DateOnly periodStart,
		TimeSpan total,
		int entryCount,
		IReadOnlyDictionary<string, TimeSpan>? moduleTimes = null)
	{
		PeriodStart = periodStart;
		Total = total;
		EntryCount = entryCount;
		ModuleTimes = moduleTimes ?? new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// The first day of the period.
	/// </summary>
	/// <remarks>
	/// For a daily report the day itself; for a weekly report the Monday of the week according
	/// to ISO 8601.
	/// </remarks>
	public DateOnly PeriodStart { get; }

	/// <summary>
	/// The summed time of all completed entries of this period.
	/// </summary>
	public TimeSpan Total { get; }

	/// <summary>
	/// The number of entries the sum comes from.
	/// </summary>
	/// <remarks>
	/// Zero for a period that exists only to fill a gap.
	/// </remarks>
	public int EntryCount { get; }

	/// <summary>
	/// The summed time of this period per module.
	/// </summary>
	/// <remarks>
	/// Looked up ignoring case. A module without time in this period is not in the dictionary;
	/// callers read it as zero. The values add up to <see cref="Total"/>.
	/// </remarks>
	public IReadOnlyDictionary<string, TimeSpan> ModuleTimes { get; }

	/// <summary>
	/// States whether any work was done in this period at all.
	/// </summary>
	public bool IsEmpty => EntryCount == 0;
}
