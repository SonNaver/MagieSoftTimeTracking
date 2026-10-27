using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Storage;

namespace Zeiterfassung.Core.Workflow;

/// <summary>
/// The established data folder with its two files.
/// </summary>
/// <remarks>
/// Settles on startup where the data lives and then mediates access to it. The difference
/// between <see cref="OpenWorkspace"/> and <see cref="CreateWorkspace"/> matters here: opening
/// an already remembered folder creates <em>nothing</em>, setting one up for the first time
/// does.
/// </remarks>
public sealed class Workspace
{
	/// <summary>
	/// The name of the time file in the data folder.
	/// </summary>
	private const string TimeFileName = "zeiten.json";

	/// <summary>
	/// The name of the module file in the data folder.
	/// </summary>
	private const string ModuleFileName = "module.json";

	/// <summary>
	/// Creates the workspace for a path that has already been checked.
	/// </summary>
	/// <param name="dataFolder">The folder in which the data files live.</param>
	private Workspace(string dataFolder)
	{
		DataFolder = dataFolder;
	}

	/// <summary>
	/// The folder in which the two travelling data files live.
	/// </summary>
	public string DataFolder { get; }

	/// <summary>
	/// The full path of the time file.
	/// </summary>
	public string TimeFilePath => Path.Combine(DataFolder, TimeFileName);

	/// <summary>
	/// The full path of the module file.
	/// </summary>
	public string ModuleFilePath => Path.Combine(DataFolder, ModuleFileName);

	/// <summary>
	/// Opens the data folder this machine has remembered.
	/// </summary>
	/// <remarks>
	/// Deliberately creates nothing. If the folder or one of the two files is missing, that is
	/// a case for the message along with the offer to pick the folder again (OBERFLAECHE.md,
	/// section 5). Quietly creating a new folder with empty files would be wrong: the folder
	/// may have been moved, or OneDrive may not have synchronized yet — the old data would then
	/// seem gone, and two states would stand side by side.
	/// </remarks>
	/// <param name="settingsFilePath">The path of the local settings file.</param>
	/// <returns>The workspace, or null on the first start on this machine.</returns>
	/// <exception cref="DataFileException">When the remembered folder or one of the two files is missing.</exception>
	public static Workspace? OpenWorkspace(string settingsFilePath)
	{
		AppSettings? settings = SettingsFile.ReadSettings(settingsFilePath);

		if (settings is null)
		{
			return null;
		}

		Workspace workspace = new(settings.DataFolder);

		workspace.RequireExistingFolder();
		workspace.RequireExistingFiles();

		return workspace;
	}

	/// <summary>
	/// Sets up a folder chosen by the user as the data folder.
	/// </summary>
	/// <remarks>
	/// Missing files are created, existing ones are <em>not</em> touched. That is the case on
	/// the second machine: there the user picks the synchronized folder in which the data of
	/// the first machine already lies — overwriting it would be the worst mistake this app
	/// could make.
	/// </remarks>
	/// <remarks>
	/// The chosen appearance is deliberately passed through, because the settings file is
	/// rewritten in full here. Without it the app would fall back to the Windows appearance as
	/// soon as the user picks the folder again — because it was moved, say. A setting should
	/// not have to survive an event that has nothing to do with it and vanish all the same.
	/// </remarks>
	/// <param name="settingsFilePath">The path of the local settings file.</param>
	/// <param name="dataFolder">The folder chosen by the user.</param>
	/// <param name="theme">The appearance to keep.</param>
	/// <returns>The established workspace.</returns>
	/// <exception cref="DataFileException">When the folder is missing or the files cannot be created.</exception>
	public static Workspace CreateWorkspace(
		string settingsFilePath,
		string dataFolder,
		ThemePreference theme = ThemePreference.System)
	{
		Workspace workspace = new(new AppSettings(dataFolder).DataFolder);

		workspace.RequireExistingFolder();
		workspace.CreateMissingFiles();

		SettingsFile.WriteSettings(settingsFilePath, new AppSettings(workspace.DataFolder, theme));

		return workspace;
	}

	/// <summary>
	/// Reads the module list from the data folder.
	/// </summary>
	/// <returns>The module list in the order of the file.</returns>
	/// <exception cref="DataFileException">When the module file is missing or does not match the expected format.</exception>
	public StudyModuleList ReadModules()
	{
		return ModuleFile.ReadModules(ModuleFilePath);
	}

	/// <summary>
	/// Writes the module list into the data folder.
	/// </summary>
	/// <remarks>
	/// The counterpart to <see cref="ReadModules"/>, ever since modules can also be maintained
	/// in the app. The file stays editable by hand alongside that.
	/// </remarks>
	/// <param name="modules">The module list to write.</param>
	/// <exception cref="DataFileException">When the folder is missing or the file cannot be written.</exception>
	public void WriteModules(StudyModuleList modules)
	{
		RequireExistingFolder();

		ModuleFile.WriteModules(ModuleFilePath, modules);
	}

	/// <summary>
	/// Reads all time entries from the data folder.
	/// </summary>
	/// <returns>The entries in the order of the file.</returns>
	/// <exception cref="DataFileException">When the time file is missing or does not match the expected format.</exception>
	public IReadOnlyList<TimeEntry> ReadEntries()
	{
		return TimeEntryFile.ReadEntries(TimeFilePath);
	}

	/// <summary>
	/// Writes all time entries into the data folder.
	/// </summary>
	/// <param name="entries">The entries to write.</param>
	/// <exception cref="DataFileException">When the time file cannot be written.</exception>
	public void WriteEntries(IEnumerable<TimeEntry> entries)
	{
		TimeEntryFile.WriteEntries(TimeFilePath, entries);
	}

	/// <summary>
	/// Checks that the data folder is present.
	/// </summary>
	/// <exception cref="DataFileException">When the folder does not exist.</exception>
	private void RequireExistingFolder()
	{
		if (!Directory.Exists(DataFolder))
		{
			throw new DataFileException(
				$"Der Ordner „{DataFolder}“ ist nicht da. Er wurde verschoben oder umbenannt, "
				+ "oder OneDrive hat ihn noch nicht synchronisiert. Bitte den Ordner neu wählen.");
		}
	}

	/// <summary>
	/// Checks that both data files lie in the folder.
	/// </summary>
	/// <exception cref="DataFileException">When one of the two files is missing.</exception>
	private void RequireExistingFiles()
	{
		foreach (string filePath in new[] { TimeFilePath, ModuleFilePath })
		{
			if (!File.Exists(filePath))
			{
				throw new DataFileException(
					$"Die Datei „{filePath}“ ist nicht da. Sie wurde verschoben oder umbenannt, "
					+ "oder OneDrive hat sie noch nicht synchronisiert. Bitte den Ordner neu wählen.");
			}
		}
	}

	/// <summary>
	/// Creates the data files that are still missing in the chosen folder.
	/// </summary>
	/// <exception cref="DataFileException">When a file cannot be created.</exception>
	private void CreateMissingFiles()
	{
		if (!File.Exists(TimeFilePath))
		{
			TimeEntryFile.CreateEmptyFile(TimeFilePath);
		}

		if (!File.Exists(ModuleFilePath))
		{
			ModuleFile.CreateEmptyFile(ModuleFilePath);
		}
	}
}
