using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Reporting;

/// <summary>
/// A period to report over — either bound optionally left open.
/// </summary>
/// <remarks>
/// The bounds are dates without a time and are read as <em>inclusive</em>: the last day counts
/// in full. An entry belongs to the day on which it <em>begins</em> — a session across
/// midnight is not split.
/// </remarks>
public sealed class DateRange
{
	/// <summary>
	/// Creates a period from bounds that have already been checked.
	/// </summary>
	/// <param name="from">The first day, or null for „no beginning“.</param>
	/// <param name="to">The last day, or null for „no end“.</param>
	private DateRange(DateOnly? from, DateOnly? to)
	{
		From = from;
		To = to;
	}

	/// <summary>
	/// The period without any bound, covering all entries.
	/// </summary>
	public static DateRange Everything { get; } = new DateRange(null, null);

	/// <summary>
	/// The first day of the period, or null when it begins open-ended.
	/// </summary>
	public DateOnly? From { get; }

	/// <summary>
	/// The last day of the period, or null when it ends open-ended.
	/// </summary>
	public DateOnly? To { get; }

	/// <summary>
	/// States whether the period has no bound at all and thereby covers everything.
	/// </summary>
	public bool IsUnbounded => From is null && To is null;

	/// <summary>
	/// Creates a period from two optionally open bounds.
	/// </summary>
	/// <param name="from">The first day, or null for „no beginning“.</param>
	/// <param name="to">The last day, or null for „no end“.</param>
	/// <returns>The checked period.</returns>
	/// <exception cref="ArgumentOutOfRangeException">When the last day lies before the first.</exception>
	public static DateRange CreateRange(DateOnly? from, DateOnly? to)
	{
		if (from is not null && to is not null && to.Value < from.Value)
		{
			throw new ArgumentOutOfRangeException(
				nameof(to),
				to,
				$"Der letzte Tag liegt vor dem ersten Tag {from.Value:d}.");
		}

		return new DateRange(from, to);
	}

	/// <summary>
	/// Checks whether an entry falls into this period.
	/// </summary>
	/// <remarks>
	/// The start day alone is decisive. An entry that begins in the period and ends after it
	/// counts in full; one that begins before it and reaches into it not at all.
	/// </remarks>
	/// <param name="entry">The entry to check.</param>
	/// <returns>True when the entry begins within the period.</returns>
	public bool ContainsEntry(TimeEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);

		DateOnly startDay = DateOnly.FromDateTime(entry.Start.Date);

		if (From is not null && startDay < From.Value)
		{
			return false;
		}

		return To is null || startDay <= To.Value;
	}
}
