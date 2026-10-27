using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Storage;

/// <summary>
/// Reads and writes <c>einstellungen.json</c>, the local settings file.
/// </summary>
/// <remarks>
/// Differs from the two other files in one fundamental point: its absence is <em>not</em> an
/// error but the first start on this machine. <see cref="ReadSettings"/> therefore simply
/// returns nothing in that case instead of throwing — the most common way through the first
/// start should not be an error path.
/// </remarks>
public static class SettingsFile
{
	/// <summary>
	/// The only format version this app reads and writes.
	/// </summary>
	/// <remarks>
	/// At 2 since 26 September 2026. Version 1 knew only the data folder; the choice between
	/// light and dark appearance added a second field, and an extension of the format costs a
	/// bump of the version. A file of version 1 is therefore rejected like any other that does
	/// not fit — the user picks the folder once more. As the file sits locally and does not
	/// travel, this affects each machine exactly once.
	/// </remarks>
	public const int SupportedFormatVersion = 2;

	/// <summary>
	/// The name of the folder below AppData in which the settings file lives.
	/// </summary>
	private const string ApplicationFolderName = "Zeiterfassung";

	/// <summary>
	/// The name of the settings file.
	/// </summary>
	private const string SettingsFileName = "einstellungen.json";

	/// <summary>
	/// Determines the path at which the settings file lives on this machine.
	/// </summary>
	/// <remarks>
	/// Deliberately the <em>Local</em> branch of AppData and not <em>Roaming</em>: the latter
	/// travels with the user profile in company and domain environments and would thereby
	/// trigger exactly the fault this location is meant to prevent — a path of one machine on the
	/// other.
	/// </remarks>
	/// <returns>The full path of the settings file.</returns>
	public static string GetDefaultFilePath()
	{
		return Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			ApplicationFolderName,
			SettingsFileName);
	}

	/// <summary>
	/// Reads the settings, if there are any on this machine yet.
	/// </summary>
	/// <param name="filePath">The path of the settings file.</param>
	/// <returns>The settings, or null on the first start on this machine.</returns>
	/// <exception cref="DataFileException">When the file is there but cannot be read or does not match the expected format.</exception>
	public static AppSettings? ReadSettings(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return null;
		}

		StoredSettingsFile storedFile = DataFileSerializer.ReadFile<StoredSettingsFile>(filePath);

		DataFileSerializer.RequireSupportedVersion(filePath, storedFile.FormatVersion, SupportedFormatVersion);

		try
		{
			return new AppSettings(storedFile.DataFolder ?? string.Empty, ReadTheme(filePath, storedFile.Theme));
		}
		catch (ArgumentException error)
		{
			throw new DataFileException(
				$"Die Einstellungen in „{filePath}“ sind fehlerhaft: {DataFileSerializer.DescribeCause(error)}",
				error);
		}
	}

	/// <summary>
	/// Interprets the text in the <c>theme</c> field as an appearance.
	/// </summary>
	/// <remarks>
	/// If the field is absent altogether, the Windows setting applies. That is the app's
	/// default and the same state it ran in before this format version — a hand-written file
	/// without this field is therefore not an error. An <em>unknown</em> value, by contrast, is
	/// rejected and not silently bent to the default; the user would otherwise take a typo for
	/// a setting that has no effect.
	/// </remarks>
	/// <param name="filePath">The path of the settings file, for the message.</param>
	/// <param name="storedTheme">The text from the file, or null.</param>
	/// <returns>The chosen appearance.</returns>
	/// <exception cref="DataFileException">When the text names no known appearance.</exception>
	private static ThemePreference ReadTheme(string filePath, string? storedTheme)
	{
		if (string.IsNullOrWhiteSpace(storedTheme))
		{
			return ThemePreference.System;
		}

		if (!Enum.TryParse(storedTheme, ignoreCase: true, out ThemePreference theme)
			|| !Enum.IsDefined(theme))
		{
			throw new DataFileException(
				$"Die Einstellungen in „{filePath}“ nennen die unbekannte Darstellung "
				+ $"„{storedTheme}“. Erlaubt sind system, light und dark.");
		}

		return theme;
	}

	/// <summary>
	/// Writes the settings and creates the folder for them if needed.
	/// </summary>
	/// <remarks>
	/// The folder below AppData does not exist yet on the first start — unlike the data folder,
	/// which the user picks themselves. It is therefore created here.
	/// </remarks>
	/// <param name="filePath">The path of the settings file.</param>
	/// <param name="settings">The settings to write.</param>
	/// <exception cref="DataFileException">When the file cannot be written.</exception>
	public static void WriteSettings(string filePath, AppSettings settings)
	{
		CreateContainingFolder(filePath);

		DataFileSerializer.WriteFile(
			filePath,
			new StoredSettingsFile
			{
				FormatVersion = SupportedFormatVersion,
				DataFolder = settings.DataFolder,

				// Lower case, like the other values in the files: the app does write this file
				// itself, but the user reads and repairs it by hand.
				Theme = settings.Theme.ToString().ToLowerInvariant(),
			});
	}

	/// <summary>
	/// Creates the folder the settings file is meant to live in.
	/// </summary>
	/// <param name="filePath">The path of the settings file.</param>
	/// <exception cref="DataFileException">When the folder cannot be created.</exception>
	private static void CreateContainingFolder(string filePath)
	{
		string? folder = Path.GetDirectoryName(filePath);

		if (string.IsNullOrEmpty(folder))
		{
			return;
		}

		try
		{
			Directory.CreateDirectory(folder);
		}
		catch (Exception error) when (error is IOException or UnauthorizedAccessException)
		{
			throw new DataFileException(
				$"Der Ordner „{folder}“ konnte nicht angelegt werden: {error.Message}",
				error);
		}
	}
}
