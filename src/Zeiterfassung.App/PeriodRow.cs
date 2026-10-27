namespace Zeiterfassung.App;

/// <summary>
/// A row of the daily or weekly report, formatted ready for display.
/// </summary>
public sealed class PeriodRow
{
	/// <summary>
	/// Creates a row of the period report.
	/// </summary>
	/// <param name="period">The label of the period.</param>
	/// <param name="total">The total time as text.</param>
	/// <param name="entryCount">The number of entries.</param>
	/// <param name="moduleTimes">The time per module as text, in the order of the module columns.</param>
	public PeriodRow(string period, string total, int entryCount, string[] moduleTimes)
	{
		Period = period;
		Total = total;
		EntryCount = entryCount;
		ModuleTimes = moduleTimes;
	}

	/// <summary>
	/// The label of the period — a date or a calendar week.
	/// </summary>
	public string Period { get; }

	/// <summary>
	/// The total time in the pattern hours:minutes:seconds.
	/// </summary>
	public string Total { get; }

	/// <summary>
	/// The number of entries in this period; zero for a period without work.
	/// </summary>
	public int EntryCount { get; }

	/// <summary>
	/// The time per module in the pattern hours:minutes:seconds.
	/// </summary>
	/// <remarks>
	/// One text per module column of the table, in the same order. The columns bind to it by
	/// position, because their number changes with the module filter.
	/// </remarks>
	public string[] ModuleTimes { get; }
}
