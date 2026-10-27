using System.Globalization;
using System.Text;
using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Reporting;

/// <summary>
/// Writes time entries as CSV text for a spreadsheet.
/// </summary>
/// <remarks>
/// Delivers the text alone and creates no file — where it is written is for the shell to
/// decide. That keeps the delicate part, the escaping per RFC 4180, testable without touching
/// a file.
/// </remarks>
public static class CsvExport
{
	/// <summary>
	/// The line ending per RFC 4180, which Excel on Windows expects as well.
	/// </summary>
	private const string LineEnding = "\r\n";

	/// <summary>
	/// The pattern for the duration as decimal hours.
	/// </summary>
	/// <remarks>
	/// Up to four decimal places. With two, a quarter of an hour would still be exact, but a
	/// session of 41 seconds would be rounded to zero.
	/// </remarks>
	private const string DecimalHoursPattern = "0.####";

	/// <summary>
	/// The column headers, in the order of the output.
	/// </summary>
	private static readonly string[] Headers =
	[
		"Datum",
		"Beginn",
		"Ende",
		"Modul",
		"Tätigkeit",
		"Dauer (Stunden)",
		"Dauer",
	];

	/// <summary>
	/// Builds the CSV text from the completed entries of a period.
	/// </summary>
	/// <remarks>
	/// The open entry stays out, as it already does in the totals: it has no
	/// duration, and an empty cell in a column one sums up is a trap. The sort is ascending —
	/// in a spreadsheet a course of work is read from top to bottom, unlike in the window's
	/// list.
	/// </remarks>
	/// <param name="entries">The entries to export.</param>
	/// <param name="range">The period to narrow down to.</param>
	/// <param name="culture">The culture for separator, date and decimal mark.</param>
	/// <param name="moduleFilter">The modules to narrow down to, or null for all.</param>
	/// <returns>The complete CSV text, with a header row even without entries.</returns>
	public static string BuildEntryCsv(
		IEnumerable<TimeEntry> entries,
		DateRange range,
		CultureInfo culture,
		ModuleFilter? moduleFilter = null)
	{
		ArgumentNullException.ThrowIfNull(entries);
		ArgumentNullException.ThrowIfNull(range);
		ArgumentNullException.ThrowIfNull(culture);

		string separator = culture.TextInfo.ListSeparator;
		StringBuilder builder = new();

		AppendRow(builder, Headers, separator);

		foreach (TimeEntry entry in SelectExportableEntries(entries, range, moduleFilter))
		{
			AppendRow(builder, BuildFields(entry, culture), separator);
		}

		return builder.ToString();
	}

	/// <summary>
	/// Counts the entries the export will write out.
	/// </summary>
	/// <remarks>
	/// Serves the message after the export. Counting the lines of the produced text would be
	/// wrong: an activity may hold a line break, and that then stands escaped in the middle of
	/// a line.
	/// </remarks>
	/// <param name="entries">The entries to export.</param>
	/// <param name="range">The period to narrow down to.</param>
	/// <param name="moduleFilter">The modules to narrow down to, or null for all.</param>
	/// <returns>The number of completed entries within the period.</returns>
	public static int CountExportedEntries(
		IEnumerable<TimeEntry> entries,
		DateRange range,
		ModuleFilter? moduleFilter = null)
	{
		ArgumentNullException.ThrowIfNull(entries);
		ArgumentNullException.ThrowIfNull(range);

		ModuleFilter filter = moduleFilter ?? ModuleFilter.All;

		return entries.Count(entry => !entry.IsOpen && range.ContainsEntry(entry) && filter.Matches(entry));
	}

	/// <summary>
	/// Counts the entries the export will leave out.
	/// </summary>
	/// <remarks>
	/// Serves the message after the export. There can be at most one open entry;
	/// the count is taken all the same, so that the message tells the truth even after a hand
	/// repair that left several open entries behind.
	/// </remarks>
	/// <param name="entries">The entries to export.</param>
	/// <param name="range">The period to narrow down to.</param>
	/// <param name="moduleFilter">The modules to narrow down to, or null for all.</param>
	/// <returns>The number of open entries within the period.</returns>
	public static int CountSkippedEntries(
		IEnumerable<TimeEntry> entries,
		DateRange range,
		ModuleFilter? moduleFilter = null)
	{
		ArgumentNullException.ThrowIfNull(entries);
		ArgumentNullException.ThrowIfNull(range);

		ModuleFilter filter = moduleFilter ?? ModuleFilter.All;

		return entries.Count(entry => entry.IsOpen && range.ContainsEntry(entry) && filter.Matches(entry));
	}

	/// <summary>
	/// Selects the entries to export and puts them into the order of the file.
	/// </summary>
	/// <param name="entries">The entries to export.</param>
	/// <param name="range">The period to narrow down to.</param>
	/// <param name="moduleFilter">The modules to narrow down to, or null for all.</param>
	/// <returns>The completed entries of the period, oldest first.</returns>
	private static IEnumerable<TimeEntry> SelectExportableEntries(
		IEnumerable<TimeEntry> entries,
		DateRange range,
		ModuleFilter? moduleFilter)
	{
		ModuleFilter filter = moduleFilter ?? ModuleFilter.All;

		return entries
			.Where(entry => !entry.IsOpen && range.ContainsEntry(entry) && filter.Matches(entry))
			.OrderBy(entry => entry.Start);
	}

	/// <summary>
	/// Forms the seven fields of a row from an entry.
	/// </summary>
	/// <param name="entry">The completed entry.</param>
	/// <param name="culture">The culture for date and decimal mark.</param>
	/// <returns>The fields in the order of the columns.</returns>
	private static string[] BuildFields(TimeEntry entry, CultureInfo culture)
	{
		TimeSpan duration = entry.Duration!.Value;

		return
		[
			entry.Start.ToString("d", culture),
			entry.Start.ToString("HH:mm:ss", culture),
			entry.End!.Value.ToString("HH:mm:ss", culture),
			entry.Module,
			entry.Note,
			duration.TotalHours.ToString(DecimalHoursPattern, culture),
			DurationFormat.FormatDuration(duration),
		];
	}

	/// <summary>
	/// Appends a row of escaped fields.
	/// </summary>
	/// <param name="builder">The text buffer being appended to.</param>
	/// <param name="fields">The fields of the row.</param>
	/// <param name="separator">The separator between the fields.</param>
	private static void AppendRow(StringBuilder builder, IReadOnlyList<string> fields, string separator)
	{
		for (int index = 0; index < fields.Count; index++)
		{
			if (index > 0)
			{
				builder.Append(separator);
			}

			builder.Append(EscapeField(fields[index], separator));
		}

		builder.Append(LineEnding);
	}

	/// <summary>
	/// Wraps a field in quotes when its content demands it.
	/// </summary>
	/// <remarks>
	/// The procedure per RFC 4180. What makes it necessary is above all the activity: it is
	/// free text, and a semicolon, a quote or a line break in it would shift the whole
	/// spreadsheet if left unescaped. A quote within the text is doubled.
	/// </remarks>
	/// <param name="field">The content of the field.</param>
	/// <param name="separator">The separator that must not stand bare within the field.</param>
	/// <returns>The field, wrapped where needed.</returns>
	private static string EscapeField(string field, string separator)
	{
		bool needsQuotes = field.Contains(separator, StringComparison.Ordinal)
			|| field.Contains('"')
			|| field.Contains('\n')
			|| field.Contains('\r');

		if (!needsQuotes)
		{
			return field;
		}

		return $"\"{field.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
	}
}
