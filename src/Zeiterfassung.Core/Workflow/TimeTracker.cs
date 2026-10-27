using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Storage;

namespace Zeiterfassung.Core.Workflow;

/// <summary>
/// Carries out play and stop and knows the running entry.
/// </summary>
/// <remarks>
/// The time file is read afresh before every write. The user repairs it by hand on purpose —
/// possibly while the app is open. Were the state from memory written back instead, the next
/// stop would undo their repair.
/// </remarks>
public sealed class TimeTracker
{
	private readonly Workspace _workspace;
	private readonly TimeProvider _timeProvider;

	private TimeEntry? _runningEntry;

	/// <summary>
	/// Creates the time tracking for a workspace.
	/// </summary>
	/// <param name="workspace">The established data folder.</param>
	/// <param name="timeProvider">The clock; the system's in operation, a stopped one in tests.</param>
	public TimeTracker(Workspace workspace, TimeProvider timeProvider)
	{
		_workspace = workspace;
		_timeProvider = timeProvider;
	}

	/// <summary>
	/// States whether a session is currently running.
	/// </summary>
	public bool IsTracking => _runningEntry is not null;

	/// <summary>
	/// The currently running entry, or null when none is running.
	/// </summary>
	public TimeEntry? RunningEntry => _runningEntry;

	/// <summary>
	/// Looks in the time file for an entry without an end.
	/// </summary>
	/// <remarks>
	/// To be called on startup, before the shell offers play. If one is found, the shell puts
	/// the choice between completing and carrying on. There can be at most one; if
	/// against expectation there are several, the file is faulty.
	/// </remarks>
	/// <returns>The open entry, or null when there is none.</returns>
	/// <exception cref="DataFileException">When the file cannot be read or holds more than one open entry.</exception>
	public TimeEntry? FindOpenEntry()
	{
		List<TimeEntry> openEntries = [.. _workspace.ReadEntries().Where(entry => entry.IsOpen)];

		if (openEntries.Count > 1)
		{
			throw new DataFileException(
				$"Die Datei „{_workspace.TimeFilePath}“ enthält {openEntries.Count} Einträge ohne "
				+ "Endzeitpunkt, es darf aber höchstens einer sein. Bitte die Datei von Hand richten.");
		}

		return openEntries.SingleOrDefault();
	}

	/// <summary>
	/// Begins a session and writes it into the time file at once.
	/// </summary>
	/// <remarks>
	/// The write happens here and not only on stop — that is the only way a session that has
	/// begun survives a crash.
	/// </remarks>
	/// <param name="module">The module name in plain text.</param>
	/// <param name="note">The activity as free text; may be empty.</param>
	/// <returns>The open entry that was begun.</returns>
	/// <exception cref="InvalidOperationException">When a session is already running.</exception>
	/// <exception cref="DataFileException">When the file cannot be read or written.</exception>
	public TimeEntry StartTracking(string module, string? note)
	{
		if (IsTracking)
		{
			throw new InvalidOperationException("Es läuft bereits eine Sitzung.");
		}

		List<TimeEntry> entries = [.. _workspace.ReadEntries()];
		TimeEntry startedEntry = TimeEntry.StartEntry(_timeProvider.GetLocalNow(), module, note);

		entries.Add(startedEntry);
		_workspace.WriteEntries(entries);
		_runningEntry = startedEntry;

		return startedEntry;
	}

	/// <summary>
	/// Ends the running session and writes it on.
	/// </summary>
	/// <returns>The completed entry.</returns>
	/// <exception cref="InvalidOperationException">When no session is currently running.</exception>
	/// <exception cref="DataFileException">When the file cannot be read or written, or no longer holds the entry.</exception>
	public TimeEntry StopTracking()
	{
		TimeEntry running = _runningEntry
			?? throw new InvalidOperationException("Es läuft gerade keine Sitzung.");

		TimeEntry stoppedEntry = CompleteEntryInFile(running.Id, _timeProvider.GetLocalNow());

		_runningEntry = null;

		return stoppedEntry;
	}

	/// <summary>
	/// Takes an open entry up again as though it had never been stopped.
	/// </summary>
	/// <remarks>
	/// Writes nothing: the entry already stands unchanged in the file, and counting carries on
	/// from exactly its stored start.
	/// </remarks>
	/// <param name="openEntry">The open entry from <see cref="FindOpenEntry"/>.</param>
	/// <exception cref="ArgumentException">When the entry is already completed.</exception>
	/// <exception cref="InvalidOperationException">When a session is already running.</exception>
	public void ResumeTracking(TimeEntry openEntry)
	{
		if (IsTracking)
		{
			throw new InvalidOperationException("Es läuft bereits eine Sitzung.");
		}

		if (!openEntry.IsOpen)
		{
			throw new ArgumentException("Der Eintrag ist bereits abgeschlossen.", nameof(openEntry));
		}

		_runningEntry = openEntry;
	}

	/// <summary>
	/// Closes an open entry with an end given by the user.
	/// </summary>
	/// <remarks>
	/// The second of the two ways for an open entry: instead of carrying on, the user enters when
	/// the session actually ended. The time tracking stays at rest afterwards.
	/// </remarks>
	/// <param name="openEntry">The open entry from <see cref="FindOpenEntry"/>.</param>
	/// <param name="end">The end given by the user.</param>
	/// <returns>The completed entry.</returns>
	/// <exception cref="DataFileException">When the end lies before the start or the file cannot be written.</exception>
	public TimeEntry CompleteOpenEntry(TimeEntry openEntry, DateTimeOffset end)
	{
		return CompleteEntryInFile(openEntry.Id, end);
	}

	/// <summary>
	/// Determines how long the running session has lasted so far.
	/// </summary>
	/// <remarks>
	/// Meant for the running display in the main window, which asks once a second.
	/// </remarks>
	/// <returns>The time elapsed so far.</returns>
	/// <exception cref="InvalidOperationException">When no session is currently running.</exception>
	public TimeSpan GetElapsedTime()
	{
		TimeEntry running = _runningEntry
			?? throw new InvalidOperationException("Es läuft gerade keine Sitzung.");

		return _timeProvider.GetLocalNow() - running.Start;
	}

	/// <summary>
	/// Reads the file afresh, closes the named entry and writes it back.
	/// </summary>
	/// <param name="entryId">The identifier of the entry to complete.</param>
	/// <param name="end">The end.</param>
	/// <returns>The completed entry.</returns>
	/// <exception cref="DataFileException">When the entry no longer stands in the file, is already completed, or the end lies before the start.</exception>
	private TimeEntry CompleteEntryInFile(Guid entryId, DateTimeOffset end)
	{
		List<TimeEntry> entries = [.. _workspace.ReadEntries()];
		TimeEntry entryToComplete = entries.SingleOrDefault(entry => entry.Id == entryId)
			?? throw new DataFileException(
				$"Der Eintrag mit der Kennung {entryId} steht nicht mehr in der Datei "
				+ $"„{_workspace.TimeFilePath}“. Wurde sie von Hand geändert?");

		try
		{
			entryToComplete.CompleteEntry(end);
		}
		catch (Exception error) when (error is ArgumentOutOfRangeException or InvalidOperationException)
		{
			throw new DataFileException(
				$"Der Eintrag konnte nicht abgeschlossen werden: {DataFileSerializer.DescribeCause(error)}",
				error);
		}

		_workspace.WriteEntries(entries);

		return entryToComplete;
	}
}
