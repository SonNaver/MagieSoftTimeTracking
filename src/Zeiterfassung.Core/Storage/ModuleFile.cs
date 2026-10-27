using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Storage;

/// <summary>
/// Reads <c>module.json</c>, the module file.
/// </summary>
/// <remarks>
/// The file still belongs to the user and may be edited by hand at any time; in addition
/// the app can also write it itself, so that modules can be created without a text editor.
/// Both ways exist side by side — the app reads the file afresh on every start and overwrites
/// it only when the user changes something in the module dialog.
/// </remarks>
public static class ModuleFile
{
	/// <summary>
	/// The only format version this app reads and writes.
	/// </summary>
	/// <remarks>
	/// At 2 since 26 September 2026. Version 1 knew only the name per module; the flag for
	/// whether a module is still offered added a second field. Unlike with the local settings
	/// file, this bump hits <em>both</em> machines: the file travels through OneDrive and is
	/// invalid on the second one until the same app version runs there.
	/// </remarks>
	public const int SupportedFormatVersion = 2;

	/// <summary>
	/// Reads the module list from the module file.
	/// </summary>
	/// <remarks>
	/// As with the time file, every error leads to the rejection of the <em>whole</em> file: a
	/// wrong format version, broken JSON, an unknown field, a module without a name, or two
	/// modules with the same name.
	/// </remarks>
	/// <param name="filePath">The path of the module file.</param>
	/// <returns>The module list in the order of the file.</returns>
	/// <exception cref="DataFileException">When the file is missing, unreadable, or does not match the expected format.</exception>
	public static StudyModuleList ReadModules(string filePath)
	{
		StoredModuleFile storedFile = DataFileSerializer.ReadFile<StoredModuleFile>(filePath);

		DataFileSerializer.RequireSupportedVersion(filePath, storedFile.FormatVersion, SupportedFormatVersion);

		return ConvertToModuleList(filePath, storedFile.Modules ?? []);
	}

	/// <summary>
	/// Creates an empty module file as a template.
	/// </summary>
	/// <remarks>
	/// The template deliberately holds <em>no</em> sample module. One would otherwise stand in
	/// the selection as though it were real, and the message „Kein Modul wählbar“ would fail to
	/// appear — although the user has entered nothing yet.
	/// </remarks>
	/// <param name="filePath">The path of the file to create.</param>
	/// <exception cref="DataFileException">When the file cannot be written.</exception>
	public static void CreateEmptyFile(string filePath)
	{
		DataFileSerializer.WriteFile(
			filePath,
			new StoredModuleFile
			{
				FormatVersion = SupportedFormatVersion,
				Modules = [],
			});
	}

	/// <summary>
	/// Writes the module list into the module file.
	/// </summary>
	/// <remarks>
	/// The file is rewritten in full, as all files of this app are, and that happens atomically
	/// through a temporary side file. This is called from the module dialog alone; tracking
	/// does not touch this file but writes into <c>zeiten.json</c>. The two therefore do not
	/// get in each other's way.
	/// </remarks>
	/// <param name="filePath">The path of the module file.</param>
	/// <param name="modules">The module list to write.</param>
	/// <exception cref="DataFileException">When the file cannot be written.</exception>
	public static void WriteModules(string filePath, StudyModuleList modules)
	{
		DataFileSerializer.WriteFile(
			filePath,
			new StoredModuleFile
			{
				FormatVersion = SupportedFormatVersion,
				Modules = [.. modules.Modules.Select(module => new StoredModule
				{
					Name = module.Name,
					IsActive = module.IsActive,
				})],
			});
	}

	/// <summary>
	/// Turns the images that were read into the module list of the data model.
	/// </summary>
	/// <remarks>
	/// Both checks live in the model — the name must not be empty, and no name may appear
	/// twice. Here their exceptions are merely translated into a rejection of the file with a
	/// displayable message.
	/// </remarks>
	/// <param name="filePath">The path of the file, for the message.</param>
	/// <param name="storedModules">The images that were read.</param>
	/// <returns>The module list of the data model.</returns>
	/// <exception cref="DataFileException">When a module has no name or a name appears twice.</exception>
	private static StudyModuleList ConvertToModuleList(string filePath, List<StoredModule> storedModules)
	{
		try
		{
			return new StudyModuleList(storedModules.Select(storedModule =>
				new StudyModule(storedModule.Name ?? string.Empty, storedModule.IsActive ?? true)));
		}
		catch (ArgumentException error)
		{
			throw new DataFileException(
				$"Die Modul-Liste in „{filePath}“ ist fehlerhaft: {DataFileSerializer.DescribeCause(error)}",
				error);
		}
	}
}
