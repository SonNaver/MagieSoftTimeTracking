namespace Zeiterfassung.Core.Model;

/// <summary>
/// Formats durations in the pattern hours:minutes:seconds.
/// </summary>
/// <remarks>
/// Lives in the core because both sides need the same form: the time file and the running
/// display in the window. Were the formatting in two places, the display could show something
/// other than what the file ends up containing.
/// </remarks>
public static class DurationFormat
{
	/// <summary>
	/// Formats a duration in the pattern hours:minutes:seconds.
	/// </summary>
	/// <remarks>
	/// The hours are deliberately computed from the total duration rather than formatted with
	/// the <c>hh</c> pattern. That one shows only the hours within a day: a session of 25 hours,
	/// forgotten overnight, would appear as 01:00:00 — a mistake that would hardly catch the eye
	/// at a glance.
	/// </remarks>
	/// <param name="duration">The duration to format.</param>
	/// <returns>The duration as text, hours in two digits or more.</returns>
	public static string FormatDuration(TimeSpan duration)
	{
		return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
	}
}
