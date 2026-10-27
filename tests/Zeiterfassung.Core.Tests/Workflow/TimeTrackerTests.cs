using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Storage;
using Zeiterfassung.Core.Workflow;

namespace Zeiterfassung.Core.Tests.Workflow;

public sealed class TimeTrackerTests : IDisposable
{
	private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

	private readonly string _root;
	private readonly Workspace _workspace;
	private readonly StoppedClock _clock;
	private readonly TimeTracker _tracker;

	public TimeTrackerTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "Zeiterfassung.Tests", Guid.NewGuid().ToString());
		string dataFolder = Path.Combine(_root, "Daten");
		Directory.CreateDirectory(dataFolder);

		_workspace = Workspace.CreateWorkspace(Path.Combine(_root, "einstellungen.json"), dataFolder);
		_clock = new StoppedClock(Noon);
		_tracker = new TimeTracker(_workspace, _clock);
	}

	/// <summary>
	/// Clears the test folder and its contents away again.
	/// </summary>
	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	[Fact]
	public void StartTracking_NothingRunning_WritesOpenEntryImmediately()
	{
		_tracker.StartTracking("Mathematik II", "Uebungsblatt 3");

		TimeEntry written = Assert.Single(_workspace.ReadEntries());

		Assert.True(written.IsOpen);
		Assert.Equal("Mathematik II", written.Module);
		Assert.Equal("Uebungsblatt 3", written.Note);
		Assert.True(_tracker.IsTracking);
	}

	[Fact]
	public void StartTracking_ExistingEntries_KeepsThem()
	{
		TimeEntry older = TimeEntry.StartEntry(Noon.AddDays(-1), "Statistik", null);
		older.CompleteEntry(Noon.AddDays(-1).AddHours(1));
		_workspace.WriteEntries([older]);

		_tracker.StartTracking("Mathematik II", null);

		Assert.Equal(2, _workspace.ReadEntries().Count);
	}

	[Fact]
	public void StartTracking_AlreadyRunning_ThrowsInvalidOperationException()
	{
		_tracker.StartTracking("Mathematik II", null);

		Assert.Throws<InvalidOperationException>(() => _tracker.StartTracking("Statistik", null));
	}

	[Fact]
	public void StopTracking_RunningSession_ClosesEntryWithElapsedTime()
	{
		_tracker.StartTracking("Mathematik II", null);
		_clock.AdvanceClock(TimeSpan.FromMinutes(90));

		TimeEntry stopped = _tracker.StopTracking();

		Assert.False(stopped.IsOpen);
		Assert.Equal(TimeSpan.FromMinutes(90), stopped.Duration);
		Assert.False(_tracker.IsTracking);
	}

	[Fact]
	public void StopTracking_RunningSession_PersistsTheClosedEntry()
	{
		_tracker.StartTracking("Mathematik II", null);
		_clock.AdvanceClock(TimeSpan.FromMinutes(45));
		_tracker.StopTracking();

		TimeEntry written = Assert.Single(_workspace.ReadEntries());

		Assert.Equal(TimeSpan.FromMinutes(45), written.Duration);
	}

	[Fact]
	public void StopTracking_NothingRunning_ThrowsInvalidOperationException()
	{
		Assert.Throws<InvalidOperationException>(() => _tracker.StopTracking());
	}

	[Fact]
	public void StopTracking_FileEditedByHandWhileRunning_KeepsTheHandwrittenEntry()
	{
		// The reason the file is read afresh before every write: the user is allowed to
		// repair it while the app is open.
		_tracker.StartTracking("Mathematik II", null);

		List<TimeEntry> entries = [.. _workspace.ReadEntries()];
		TimeEntry addedByHand = TimeEntry.StartEntry(Noon.AddDays(-2), "Statistik", "von Hand ergaenzt");
		addedByHand.CompleteEntry(Noon.AddDays(-2).AddHours(2));
		entries.Insert(0, addedByHand);
		_workspace.WriteEntries(entries);

		_clock.AdvanceClock(TimeSpan.FromMinutes(10));
		_tracker.StopTracking();

		Assert.Contains(_workspace.ReadEntries(), entry => entry.Id == addedByHand.Id);
	}

	[Fact]
	public void StopTracking_EntryDeletedByHandWhileRunning_ThrowsDataFileException()
	{
		_tracker.StartTracking("Mathematik II", null);
		_workspace.WriteEntries([]);

		Assert.Throws<DataFileException>(() => _tracker.StopTracking());
	}

	[Fact]
	public void FindOpenEntry_NoOpenEntry_ReturnsNothing()
	{
		Assert.Null(_tracker.FindOpenEntry());
	}

	[Fact]
	public void FindOpenEntry_OneOpenEntry_FindsIt()
	{
		TimeEntry openEntry = TimeEntry.StartEntry(Noon.AddHours(-3), "Mathematik II", null);
		_workspace.WriteEntries([openEntry]);

		TimeEntry? found = _tracker.FindOpenEntry();

		Assert.NotNull(found);
		Assert.Equal(openEntry.Id, found.Id);
	}

	[Fact]
	public void FindOpenEntry_SeveralOpenEntries_ThrowsDataFileException()
	{
		_workspace.WriteEntries([
			TimeEntry.StartEntry(Noon.AddHours(-3), "Mathematik II", null),
			TimeEntry.StartEntry(Noon.AddHours(-1), "Statistik", null)]);

		DataFileException error = Assert.Throws<DataFileException>(() => _tracker.FindOpenEntry());

		Assert.Contains("höchstens einer", error.Message);
	}

	[Fact]
	public void ResumeTracking_OpenEntryFromEarlier_CountsOnFromItsStoredStart()
	{
		TimeEntry openEntry = TimeEntry.StartEntry(Noon.AddHours(-3), "Mathematik II", null);
		_workspace.WriteEntries([openEntry]);

		_tracker.ResumeTracking(openEntry);

		Assert.True(_tracker.IsTracking);
		Assert.Equal(TimeSpan.FromHours(3), _tracker.GetElapsedTime());
	}

	[Fact]
	public void ResumeTracking_ThenStop_ClosesTheSameEntry()
	{
		TimeEntry openEntry = TimeEntry.StartEntry(Noon.AddHours(-3), "Mathematik II", null);
		_workspace.WriteEntries([openEntry]);
		_tracker.ResumeTracking(openEntry);

		TimeEntry stopped = _tracker.StopTracking();

		Assert.Equal(openEntry.Id, stopped.Id);
		Assert.Equal(TimeSpan.FromHours(3), stopped.Duration);
		Assert.Single(_workspace.ReadEntries());
	}

	[Fact]
	public void CompleteOpenEntry_EndGivenByUser_ClosesEntryWithThatTime()
	{
		TimeEntry openEntry = TimeEntry.StartEntry(Noon.AddHours(-5), "Mathematik II", null);
		_workspace.WriteEntries([openEntry]);

		TimeEntry completed = _tracker.CompleteOpenEntry(openEntry, Noon.AddHours(-3));

		Assert.Equal(TimeSpan.FromHours(2), completed.Duration);
		Assert.False(_tracker.IsTracking);
	}

	[Fact]
	public void CompleteOpenEntry_EndBeforeStart_ThrowsDataFileException()
	{
		TimeEntry openEntry = TimeEntry.StartEntry(Noon.AddHours(-5), "Mathematik II", null);
		_workspace.WriteEntries([openEntry]);

		Assert.Throws<DataFileException>(() => _tracker.CompleteOpenEntry(openEntry, Noon.AddHours(-6)));
	}

	[Fact]
	public void GetElapsedTime_NothingRunning_ThrowsInvalidOperationException()
	{
		Assert.Throws<InvalidOperationException>(() => _tracker.GetElapsedTime());
	}

	[Fact]
	public void GetElapsedTime_RunningSession_GrowsWithTheClock()
	{
		_tracker.StartTracking("Mathematik II", null);

		_clock.AdvanceClock(TimeSpan.FromSeconds(30));

		Assert.Equal(TimeSpan.FromSeconds(30), _tracker.GetElapsedTime());
	}
}
