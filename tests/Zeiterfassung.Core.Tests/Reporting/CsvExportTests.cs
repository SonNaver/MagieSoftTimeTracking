using System.Globalization;
using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Reporting;

namespace Zeiterfassung.Core.Tests.Reporting;

public sealed class CsvExportTests
{
	private static readonly TimeSpan Offset = TimeSpan.FromHours(2);
	private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
	private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

	[Fact]
	public void BuildEntryCsv_WithoutAnyEntry_ReturnsOnlyTheHeaderRow()
	{
		string csv = CsvExport.BuildEntryCsv([], DateRange.Everything, German);

		Assert.Equal("Datum;Beginn;Ende;Modul;Tätigkeit;Dauer (Stunden);Dauer\r\n", csv);
	}

	[Fact]
	public void BuildEntryCsv_GermanCulture_SeparatesWithSemicolonAndDecimalComma()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 09:30", "Mathematik II", "Blatt 3")];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, German);

		Assert.Equal("21.09.2026;08:00:00;09:30:00;Mathematik II;Blatt 3;1,5;01:30:00", ReadLine(csv, 1));
	}

	[Fact]
	public void BuildEntryCsv_EnglishCulture_SeparatesWithCommaAndDecimalPoint()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 09:30", "Mathematik II", "Blatt 3")];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, English);

		Assert.Equal("9/21/2026,08:00:00,09:30:00,Mathematik II,Blatt 3,1.5,01:30:00", ReadLine(csv, 1));
	}

	[Fact]
	public void BuildEntryCsv_NoteContainingTheSeparator_WrapsTheFieldInQuotes()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", "Kapitel 1; Kapitel 2")];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, German);

		Assert.Contains("\"Kapitel 1; Kapitel 2\"", ReadLine(csv, 1), StringComparison.Ordinal);
		Assert.Equal(7, CountFields(ReadLine(csv, 1)));
	}

	[Fact]
	public void BuildEntryCsv_NoteContainingAQuote_DoublesItAndWrapsTheField()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", "Kapitel \"Grundlagen\"")];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, German);

		Assert.Contains("\"Kapitel \"\"Grundlagen\"\"\"", ReadLine(csv, 1), StringComparison.Ordinal);
	}

	[Fact]
	public void BuildEntryCsv_NoteContainingALineBreak_WrapsTheFieldInQuotes()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", "Erste Zeile\nZweite Zeile")];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, German);

		Assert.Contains("\"Erste Zeile\nZweite Zeile\"", csv, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildEntryCsv_OpenEntry_IsLeftOut()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", null),
			CreateOpenEntry("2026-09-22 08:00", "Statistik"),
		];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, German);

		Assert.Equal(2, CountLines(csv));
	}

	[Fact]
	public void BuildEntryCsv_EntriesOutsideTheRange_AreLeftOut()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-19 08:00", "2026-09-19 09:00", "Statistik", null),
			CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", null),
		];
		DateRange range = DateRange.CreateRange(new DateOnly(2026, 9, 20), null);

		string csv = CsvExport.BuildEntryCsv(entries, range, German);

		Assert.Equal(2, CountLines(csv));
		Assert.StartsWith("21.09.2026", ReadLine(csv, 1), StringComparison.Ordinal);
	}

	[Fact]
	public void BuildEntryCsv_SeveralEntries_PutsTheOldestFirst()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-24 08:00", "2026-09-24 09:00", "Statistik", null),
			CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Mathematik II", null),
		];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, German);

		Assert.StartsWith("21.09.2026", ReadLine(csv, 1), StringComparison.Ordinal);
		Assert.StartsWith("24.09.2026", ReadLine(csv, 2), StringComparison.Ordinal);
	}

	[Fact]
	public void BuildEntryCsv_WithAModuleFilter_WritesOnlyTheSelectedModules()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", null),
			CreateEntry("2026-09-22 08:00", "2026-09-22 09:00", "Mathematik II", null),
		];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, German, new ModuleFilter(["statistik"]));

		Assert.Equal(2, CountLines(csv));
		Assert.Contains("Statistik", ReadLine(csv, 1), StringComparison.Ordinal);
		Assert.Equal(
			1,
			CsvExport.CountExportedEntries(entries, DateRange.Everything, new ModuleFilter(["statistik"])));
	}

	[Fact]
	public void BuildEntryCsv_ShortEntry_KeepsTheSecondsInTheDecimalHours()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 08:00:41", "Statistik", null)];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, German);

		Assert.Contains(";0,0114;00:00:41", ReadLine(csv, 1), StringComparison.Ordinal);
	}

	[Fact]
	public void BuildEntryCsv_EmptyNote_StaysAnEmptyField()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", null)];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, German);

		Assert.Contains("Statistik;;1;01:00:00", ReadLine(csv, 1), StringComparison.Ordinal);
	}

	[Fact]
	public void BuildEntryCsv_EveryRow_EndsWithCarriageReturnAndLineFeed()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", null)];

		string csv = CsvExport.BuildEntryCsv(entries, DateRange.Everything, German);

		Assert.EndsWith("\r\n", csv, StringComparison.Ordinal);
		Assert.Equal(2, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
	}

	[Fact]
	public void CountExportedEntries_MixedEntries_CountsOnlyTheClosedOnesInTheRange()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-19 08:00", "2026-09-19 09:00", "Statistik", null),
			CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", null),
			CreateOpenEntry("2026-09-22 08:00", "Statistik"),
		];
		DateRange range = DateRange.CreateRange(new DateOnly(2026, 9, 20), null);

		Assert.Equal(1, CsvExport.CountExportedEntries(entries, range));
	}

	[Fact]
	public void CountExportedEntries_NoteWithALineBreak_StillCountsOneEntry()
	{
		TimeEntry[] entries = [CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", "Erste\nZweite")];

		Assert.Equal(1, CsvExport.CountExportedEntries(entries, DateRange.Everything));
	}

	[Fact]
	public void CountSkippedEntries_OpenEntryInTheRange_CountsIt()
	{
		TimeEntry[] entries =
		[
			CreateEntry("2026-09-21 08:00", "2026-09-21 09:00", "Statistik", null),
			CreateOpenEntry("2026-09-22 08:00", "Statistik"),
		];

		Assert.Equal(1, CsvExport.CountSkippedEntries(entries, DateRange.Everything));
	}

	[Fact]
	public void CountSkippedEntries_OpenEntryOutsideTheRange_DoesNotCountIt()
	{
		TimeEntry[] entries = [CreateOpenEntry("2026-09-22 08:00", "Statistik")];
		DateRange range = DateRange.CreateRange(null, new DateOnly(2026, 9, 21));

		Assert.Equal(0, CsvExport.CountSkippedEntries(entries, range));
	}

	/// <summary>
	/// Creates a completed entry from two time values.
	/// </summary>
	/// <param name="start">The start in the pattern „yyyy-mm-dd hh:mm“, or with seconds.</param>
	/// <param name="end">The end in the same pattern.</param>
	/// <param name="module">The module name.</param>
	/// <param name="note">The activity as free text.</param>
	/// <returns>The finished entry.</returns>
	private static TimeEntry CreateEntry(string start, string end, string module, string? note)
	{
		return TimeEntry.RestoreEntry(Guid.NewGuid(), ParseMoment(start), ParseMoment(end), module, note);
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
	/// <param name="moment">The time value, with or without seconds.</param>
	/// <returns>The point in time as local time with an offset.</returns>
	private static DateTimeOffset ParseMoment(string moment)
	{
		string[] patterns = ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"];
		DateTime local = DateTime.ParseExact(moment, patterns, CultureInfo.InvariantCulture);

		return new DateTimeOffset(local, Offset);
	}

	/// <summary>
	/// Fetches one line out of the produced text.
	/// </summary>
	/// <param name="csv">The produced text.</param>
	/// <param name="index">The number of the line, zero-based.</param>
	/// <returns>The line without its line ending.</returns>
	private static string ReadLine(string csv, int index)
	{
		return csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[index];
	}

	/// <summary>
	/// Counts the lines of the produced text, header row included.
	/// </summary>
	/// <param name="csv">The produced text.</param>
	/// <returns>The number of lines.</returns>
	private static int CountLines(string csv)
	{
		return csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length;
	}

	/// <summary>
	/// Counts the fields of a line, taking the quotes into account.
	/// </summary>
	/// <param name="line">The line to examine.</param>
	/// <returns>The number of fields.</returns>
	private static int CountFields(string line)
	{
		int fields = 1;
		bool isQuoted = false;

		foreach (char character in line)
		{
			if (character == '"')
			{
				isQuoted = !isQuoted;
			}
			else if (character == ';' && !isQuoted)
			{
				fields++;
			}
		}

		return fields;
	}
}
