namespace Zeiterfassung.App;

/// <summary>
/// A row of the entry list, formatted ready for display.
/// </summary>
public sealed class EntryRow
{
	/// <summary>
	/// Creates a row of the entry list.
	/// </summary>
	/// <param name="day">The day of the entry.</param>
	/// <param name="period">The time of day, from and to.</param>
	/// <param name="module">The module name.</param>
	/// <param name="note">The activity as free text.</param>
	/// <param name="duration">The duration as text.</param>
	public EntryRow(string day, string period, string module, string note, string duration)
	{
		Day = day;
		Period = period;
		Module = module;
		Note = note;
		Duration = duration;
	}

	/// <summary>
	/// The day of the entry with the weekday.
	/// </summary>
	public string Day { get; }

	/// <summary>
	/// The time of day, from and to; without an end for an open entry.
	/// </summary>
	public string Period { get; }

	/// <summary>
	/// The module name.
	/// </summary>
	public string Module { get; }

	/// <summary>
	/// The activity as free text, possibly empty.
	/// </summary>
	public string Note { get; }

	/// <summary>
	/// The duration in the pattern hours:minutes:seconds, or the hint about the running session.
	/// </summary>
	public string Duration { get; }
}
