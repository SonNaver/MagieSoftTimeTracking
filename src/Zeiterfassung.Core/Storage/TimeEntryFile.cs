using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Storage;

/// <summary>
/// Reads and writes <c>zeiten.json</c>, the time file.
/// </summary>
public static class TimeEntryFile
{
	/// <summary>
	/// The only format version this app reads and writes.
	/// </summary>
	public const int SupportedFormatVersion = 1;

	/// <summary>
	/// Reads all time entries from the time file.
	/// </summary>
	/// <remarks>
	/// Every error leads to the rejection of the <em>whole</em> file — a wrong format version,
	/// broken JSON, an unknown field, or an entry that is impossible in itself. Nothing is ever
	/// interpreted „as well as possible“, and a part of the entries is never taken over.
	/// </remarks>
	/// <param name="filePath">The path of the time file.</param>
	/// <returns>The entries in the order of the file.</returns>
	/// <exception cref="DataFileException">When the file is missing, unreadable, or does not match the expected format.</exception>
	public static IReadOnlyList<TimeEntry> ReadEntries(string filePath)
	{
		StoredTimeFile storedFile = DataFileSerializer.ReadFile<StoredTimeFile>(filePath);

		DataFileSerializer.RequireSupportedVersion(filePath, storedFile.FormatVersion, SupportedFormatVersion);

		return ConvertToEntries(filePath, storedFile.Entries ?? []);
	}

	/// <summary>
	/// Writes all time entries into the time file.
	/// </summary>
	/// <remarks>
	/// The file is rewritten in full — the array allows nothing else — and atomically, through
	/// <see cref="AtomicFileWriter"/>. The duration is computed afresh from start and end and
	/// written along; a contradictory duration from a hand repair thereby disappears by itself.
	/// </remarks>
	/// <param name="filePath">The path of the time file.</param>
	/// <param name="entries">The entries to write.</param>
	/// <exception cref="DataFileException">When the file cannot be written.</exception>
	public static void WriteEntries(string filePath, IEnumerable<TimeEntry> entries)
	{
		StoredTimeFile storedFile = new()
		{
			FormatVersion = SupportedFormatVersion,
			Entries = [.. entries.Select(ConvertToStoredEntry)],
		};

		DataFileSerializer.WriteFile(filePath, storedFile);
	}

	/// <summary>
	/// Creates an empty time file.
	/// </summary>
	/// <param name="filePath">The path of the file to create.</param>
	/// <exception cref="DataFileException">When the file cannot be written.</exception>
	public static void CreateEmptyFile(string filePath)
	{
		WriteEntries(filePath, []);
	}

	/// <summary>
	/// Turns the images that were read into entries of the data model.
	/// </summary>
	/// <remarks>
	/// This is where an entry damaged by hand comes to light: the model checks for itself
	/// whether the values fit together when restoring, and the exception becomes a rejection of
	/// the whole file with the identifier of the culprit in the message.
	/// </remarks>
	/// <param name="filePath">The path of the file, for the message.</param>
	/// <param name="storedEntries">The images that were read.</param>
	/// <returns>The entries of the data model.</returns>
	/// <exception cref="DataFileException">When an entry lacks required values or is impossible in itself.</exception>
	private static IReadOnlyList<TimeEntry> ConvertToEntries(string filePath, List<StoredTimeEntry> storedEntries)
	{
		List<TimeEntry> entries = [];

		foreach (StoredTimeEntry storedEntry in storedEntries)
		{
			entries.Add(ConvertToEntry(filePath, storedEntry));
		}

		return entries;
	}

	/// <summary>
	/// Turns a single image that was read into an entry of the data model.
	/// </summary>
	/// <param name="filePath">The path of the file, for the message.</param>
	/// <param name="storedEntry">The image that was read.</param>
	/// <returns>The entry of the data model.</returns>
	/// <exception cref="DataFileException">When required values are missing or the values do not fit together.</exception>
	private static TimeEntry ConvertToEntry(string filePath, StoredTimeEntry storedEntry)
	{
		string entryLabel = storedEntry.Id is null
			? "ein Eintrag ohne Kennung"
			: $"der Eintrag mit der Kennung {storedEntry.Id}";

		if (storedEntry.Start is null)
		{
			throw new DataFileException(
				$"In der Datei „{filePath}“ hat {entryLabel} keinen Startzeitpunkt.");
		}

		try
		{
			return TimeEntry.RestoreEntry(
				storedEntry.Id ?? Guid.Empty,
				storedEntry.Start.Value,
				storedEntry.End,
				storedEntry.Module ?? string.Empty,
				storedEntry.Note);
		}
		catch (Exception error) when (error is ArgumentException or ArgumentOutOfRangeException)
		{
			throw new DataFileException(
				$"In der Datei „{filePath}“ ist {entryLabel} fehlerhaft: {DataFileSerializer.DescribeCause(error)}",
				error);
		}
	}

	/// <summary>
	/// Turns an entry of the data model into its image for the file.
	/// </summary>
	/// <param name="entry">The entry to write.</param>
	/// <returns>The image as it goes into the file.</returns>
	private static StoredTimeEntry ConvertToStoredEntry(TimeEntry entry)
	{
		return new StoredTimeEntry
		{
			Id = entry.Id,
			Start = entry.Start,
			End = entry.End,
			Module = entry.Module,
			Note = entry.Note,
			Duration = entry.Duration is null ? null : DurationFormat.FormatDuration(entry.Duration.Value),
		};
	}
}
