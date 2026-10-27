using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Storage;

namespace Zeiterfassung.Core.Tests.Storage;

public sealed class TimeEntryFileTests : IDisposable
{
	private static readonly DateTimeOffset Start = new(2026, 9, 24, 14, 0, 0, TimeSpan.FromHours(2));

	private readonly string _folder;
	private readonly string _filePath;

	public TimeEntryFileTests()
	{
		_folder = Path.Combine(Path.GetTempPath(), "Zeiterfassung.Tests", Guid.NewGuid().ToString());
		Directory.CreateDirectory(_folder);
		_filePath = Path.Combine(_folder, "zeiten.json");
	}

	/// <summary>
	/// Clears the test folder and its contents away again.
	/// </summary>
	public void Dispose()
	{
		Directory.Delete(_folder, recursive: true);
	}

	[Fact]
	public void WriteAndReadEntries_CompletedEntry_KeepsAllValues()
	{
		TimeEntry written = TimeEntry.StartEntry(Start, "Mathematik II", "Uebungsblatt 3");
		written.CompleteEntry(Start.AddMinutes(90));

		TimeEntryFile.WriteEntries(_filePath, [written]);
		TimeEntry read = Assert.Single(TimeEntryFile.ReadEntries(_filePath));

		Assert.Equal(written.Id, read.Id);
		Assert.Equal(written.Start, read.Start);
		Assert.Equal(written.End, read.End);
		Assert.Equal("Mathematik II", read.Module);
		Assert.Equal("Uebungsblatt 3", read.Note);
		Assert.Equal(TimeSpan.FromMinutes(90), read.Duration);
	}

	[Fact]
	public void WriteAndReadEntries_OpenEntry_StaysOpen()
	{
		TimeEntry written = TimeEntry.StartEntry(Start, "Mathematik II", null);

		TimeEntryFile.WriteEntries(_filePath, [written]);
		TimeEntry read = Assert.Single(TimeEntryFile.ReadEntries(_filePath));

		Assert.True(read.IsOpen);
		Assert.Null(read.End);
	}

	[Fact]
	public void WriteAndReadEntries_SeveralEntries_KeepsOrder()
	{
		TimeEntry first = TimeEntry.StartEntry(Start, "Statistik", null);
		first.CompleteEntry(Start.AddMinutes(30));
		TimeEntry second = TimeEntry.StartEntry(Start.AddHours(2), "Mathematik II", null);

		TimeEntryFile.WriteEntries(_filePath, [first, second]);
		IReadOnlyList<TimeEntry> read = TimeEntryFile.ReadEntries(_filePath);

		Assert.Equal(["Statistik", "Mathematik II"], read.Select(entry => entry.Module));
	}

	[Fact]
	public void WriteEntries_SessionLongerThanADay_WritesTotalHoursNotHoursWithinDay()
	{
		TimeEntry entry = TimeEntry.StartEntry(Start, "Mathematik II", null);
		entry.CompleteEntry(Start.AddHours(25));

		TimeEntryFile.WriteEntries(_filePath, [entry]);

		Assert.Contains("25:00:00", File.ReadAllText(_filePath));
	}

	[Fact]
	public void WriteEntries_Always_LeavesNoTemporaryFileBehind()
	{
		TimeEntryFile.WriteEntries(_filePath, [TimeEntry.StartEntry(Start, "Mathematik II", null)]);

		Assert.Equal([_filePath], Directory.GetFiles(_folder));
	}

	[Fact]
	public void WriteEntries_ExistingFile_ReplacesItCompletely()
	{
		TimeEntryFile.WriteEntries(_filePath, [TimeEntry.StartEntry(Start, "Statistik", null)]);
		TimeEntryFile.WriteEntries(_filePath, [TimeEntry.StartEntry(Start, "Mathematik II", null)]);

		TimeEntry read = Assert.Single(TimeEntryFile.ReadEntries(_filePath));

		Assert.Equal("Mathematik II", read.Module);
	}

	[Fact]
	public void CreateEmptyFile_NewFile_CanBeReadAndHasNoEntries()
	{
		TimeEntryFile.CreateEmptyFile(_filePath);

		Assert.Empty(TimeEntryFile.ReadEntries(_filePath));
	}

	[Fact]
	public void ReadEntries_MissingFile_ThrowsDataFileException()
	{
		DataFileException error = Assert.Throws<DataFileException>(() => TimeEntryFile.ReadEntries(_filePath));

		Assert.Contains("nicht gefunden", error.Message);
	}

	[Fact]
	public void ReadEntries_BrokenJson_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"formatVersion\": 1, \"entries\": [");

		Assert.Throws<DataFileException>(() => TimeEntryFile.ReadEntries(_filePath));
	}

	[Fact]
	public void ReadEntries_MissingFormatVersion_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"entries\": [] }");

		DataFileException error = Assert.Throws<DataFileException>(() => TimeEntryFile.ReadEntries(_filePath));

		Assert.Contains("keine Formatversion", error.Message);
	}

	[Fact]
	public void ReadEntries_WrongFormatVersion_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"formatVersion\": 2, \"entries\": [] }");

		DataFileException error = Assert.Throws<DataFileException>(() => TimeEntryFile.ReadEntries(_filePath));

		Assert.Contains("Formatversion 2", error.Message);
	}

	[Fact]
	public void ReadEntries_UnknownField_ThrowsDataFileException()
	{
		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 1, \"entries\": [], \"comment\": \"von Hand ergaenzt\" }");

		Assert.Throws<DataFileException>(() => TimeEntryFile.ReadEntries(_filePath));
	}

	[Fact]
	public void ReadEntries_EndBeforeStart_ThrowsDataFileExceptionNamingTheEntry()
	{
		Guid id = Guid.NewGuid();
		WriteRawEntry(id, "2026-09-24T14:00:00+02:00", "2026-09-24T13:00:00+02:00");

		DataFileException error = Assert.Throws<DataFileException>(() => TimeEntryFile.ReadEntries(_filePath));

		Assert.Contains(id.ToString(), error.Message);
	}

	[Fact]
	public void ReadEntries_EntryWithoutModule_ThrowsDataFileException()
	{
		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 1, \"entries\": [ { \"id\": \"" + Guid.NewGuid()
			+ "\", \"start\": \"2026-09-24T14:00:00+02:00\" } ] }");

		Assert.Throws<DataFileException>(() => TimeEntryFile.ReadEntries(_filePath));
	}

	[Fact]
	public void ReadEntries_TimestampWithoutOffset_IsReadAsLocalTime()
	{
		WriteRawEntry(Guid.NewGuid(), "2026-09-24T14:00:00", null);

		TimeEntry read = Assert.Single(TimeEntryFile.ReadEntries(_filePath));

		Assert.Equal(new DateTime(2026, 9, 24, 14, 0, 0), read.Start.DateTime);
		Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(read.Start), read.Start.Offset);
	}

	[Fact]
	public void ReadEntries_StoredDurationContradictsTimes_UsesComputedDuration()
	{
		Guid id = Guid.NewGuid();
		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 1, \"entries\": [ { \"id\": \"" + id
			+ "\", \"start\": \"2026-09-24T14:00:00+02:00\", \"end\": \"2026-09-24T15:00:00+02:00\""
			+ ", \"module\": \"Mathematik II\", \"duration\": \"09:99:99\" } ] }");

		TimeEntry read = Assert.Single(TimeEntryFile.ReadEntries(_filePath));

		Assert.Equal(TimeSpan.FromHours(1), read.Duration);
	}

	[Fact]
	public void WriteEntries_AfterRepairedDuration_WritesComputedValueBack()
	{
		Guid id = Guid.NewGuid();
		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 1, \"entries\": [ { \"id\": \"" + id
			+ "\", \"start\": \"2026-09-24T14:00:00+02:00\", \"end\": \"2026-09-24T15:00:00+02:00\""
			+ ", \"module\": \"Mathematik II\", \"duration\": \"09:99:99\" } ] }");

		TimeEntryFile.WriteEntries(_filePath, TimeEntryFile.ReadEntries(_filePath));

		Assert.Contains("01:00:00", File.ReadAllText(_filePath));
		Assert.DoesNotContain("09:99:99", File.ReadAllText(_filePath));
	}

	/// <summary>
	/// Writes a time file with exactly one entry as raw text.
	/// </summary>
	/// <remarks>
	/// Needed to produce cases that cannot arise through the model at all — an end before the
	/// start, say, or a timestamp without an offset, such as a hand repair leaves behind.
	/// </remarks>
	/// <param name="id">The identifier of the entry.</param>
	/// <param name="start">The start as text, exactly as it is to stand in the file.</param>
	/// <param name="end">The end as text, or null for an open entry.</param>
	private void WriteRawEntry(Guid id, string start, string? end)
	{
		string endField = end is null ? string.Empty : $", \"end\": \"{end}\"";

		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 1, \"entries\": [ { \"id\": \"" + id + "\", \"start\": \"" + start + "\""
			+ endField + ", \"module\": \"Mathematik II\" } ] }");
	}
}
