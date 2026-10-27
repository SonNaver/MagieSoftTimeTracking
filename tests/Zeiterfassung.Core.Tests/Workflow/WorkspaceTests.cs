using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Storage;
using Zeiterfassung.Core.Workflow;

namespace Zeiterfassung.Core.Tests.Workflow;

public sealed class WorkspaceTests : IDisposable
{
	private readonly string _root;
	private readonly string _dataFolder;
	private readonly string _settingsPath;

	public WorkspaceTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "Zeiterfassung.Tests", Guid.NewGuid().ToString());
		_dataFolder = Path.Combine(_root, "OneDrive", "Zeiterfassung");
		_settingsPath = Path.Combine(_root, "AppData", "einstellungen.json");
		Directory.CreateDirectory(_dataFolder);
	}

	/// <summary>
	/// Clears the test folder and its contents away again.
	/// </summary>
	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	[Fact]
	public void OpenWorkspace_NoSettingsYet_ReturnsNothing()
	{
		Assert.Null(Workspace.OpenWorkspace(_settingsPath));
	}

	[Fact]
	public void OpenWorkspace_SettingsAndFilesPresent_ReturnsWorkspace()
	{
		Workspace.CreateWorkspace(_settingsPath, _dataFolder);

		Workspace? opened = Workspace.OpenWorkspace(_settingsPath);

		Assert.NotNull(opened);
		Assert.Equal(_dataFolder, opened.DataFolder);
		Assert.Equal(Path.Combine(_dataFolder, "zeiten.json"), opened.TimeFilePath);
		Assert.Equal(Path.Combine(_dataFolder, "module.json"), opened.ModuleFilePath);
	}

	[Fact]
	public void OpenWorkspace_FolderIsGone_ThrowsAndOffersToChooseAgain()
	{
		Workspace.CreateWorkspace(_settingsPath, _dataFolder);
		Directory.Delete(_dataFolder, recursive: true);

		DataFileException error = Assert.Throws<DataFileException>(() => Workspace.OpenWorkspace(_settingsPath));

		Assert.Contains("neu wählen", error.Message);
	}

	[Fact]
	public void OpenWorkspace_TimeFileIsGone_ThrowsInsteadOfCreatingIt()
	{
		Workspace.CreateWorkspace(_settingsPath, _dataFolder);
		File.Delete(Path.Combine(_dataFolder, "zeiten.json"));

		Assert.Throws<DataFileException>(() => Workspace.OpenWorkspace(_settingsPath));
		Assert.False(File.Exists(Path.Combine(_dataFolder, "zeiten.json")));
	}

	[Fact]
	public void OpenWorkspace_ModuleFileIsGone_ThrowsInsteadOfCreatingIt()
	{
		Workspace.CreateWorkspace(_settingsPath, _dataFolder);
		File.Delete(Path.Combine(_dataFolder, "module.json"));

		Assert.Throws<DataFileException>(() => Workspace.OpenWorkspace(_settingsPath));
		Assert.False(File.Exists(Path.Combine(_dataFolder, "module.json")));
	}

	[Fact]
	public void CreateWorkspace_EmptyFolder_CreatesBothFiles()
	{
		Workspace workspace = Workspace.CreateWorkspace(_settingsPath, _dataFolder);

		Assert.True(File.Exists(workspace.TimeFilePath));
		Assert.True(File.Exists(workspace.ModuleFilePath));
		Assert.Empty(workspace.ReadEntries());
		Assert.True(workspace.ReadModules().IsEmpty);
	}

	[Fact]
	public void CreateWorkspace_EmptyFolder_RemembersFolderForNextStart()
	{
		Workspace.CreateWorkspace(_settingsPath, _dataFolder);

		AppSettings? settings = SettingsFile.ReadSettings(_settingsPath);

		Assert.NotNull(settings);
		Assert.Equal(_dataFolder, settings.DataFolder);
	}

	[Fact]
	public void CreateWorkspace_FolderAlreadyHoldsData_LeavesItUntouched()
	{
		// The case on the second machine: the chosen folder already holds the synchronized
		// data of the first. Overwriting it would be the worst mistake this app could
		// make.
		Workspace first = Workspace.CreateWorkspace(_settingsPath, _dataFolder);
		TimeEntry existing = TimeEntry.StartEntry(
			new DateTimeOffset(2026, 9, 24, 14, 0, 0, TimeSpan.FromHours(2)),
			"Mathematik II",
			"vom PC");
		first.WriteEntries([existing]);
		File.WriteAllText(
			first.ModuleFilePath,
			"{ \"formatVersion\": 2, \"modules\": [ { \"name\": \"Mathematik II\" } ] }");

		string otherSettingsPath = Path.Combine(_root, "AppData2", "einstellungen.json");
		Workspace second = Workspace.CreateWorkspace(otherSettingsPath, _dataFolder);

		Assert.Equal(existing.Id, Assert.Single(second.ReadEntries()).Id);
		Assert.Equal("Mathematik II", Assert.Single(second.ReadModules().Modules).Name);
	}

	[Fact]
	public void CreateWorkspace_FolderDoesNotExist_ThrowsInsteadOfCreatingIt()
	{
		string missingFolder = Path.Combine(_root, "gibtesnicht");

		Assert.Throws<DataFileException>(() => Workspace.CreateWorkspace(_settingsPath, missingFolder));
		Assert.False(Directory.Exists(missingFolder));
	}
}
