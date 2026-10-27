using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Storage;

namespace Zeiterfassung.Core.Tests.Storage;

public sealed class SettingsFileTests : IDisposable
{
	private readonly string _folder;
	private readonly string _filePath;

	public SettingsFileTests()
	{
		_folder = Path.Combine(Path.GetTempPath(), "Zeiterfassung.Tests", Guid.NewGuid().ToString());
		Directory.CreateDirectory(_folder);
		_filePath = Path.Combine(_folder, "einstellungen.json");
	}

	/// <summary>
	/// Clears the test folder and its contents away again.
	/// </summary>
	public void Dispose()
	{
		Directory.Delete(_folder, recursive: true);
	}

	[Fact]
	public void ReadSettings_NoFileYet_ReturnsNothingInsteadOfThrowing()
	{
		Assert.Null(SettingsFile.ReadSettings(_filePath));
	}

	[Fact]
	public void WriteAndReadSettings_DataFolder_KeepsThePath()
	{
		SettingsFile.WriteSettings(_filePath, new AppSettings(@"C:\Users\test\OneDrive\Zeiterfassung"));

		AppSettings? read = SettingsFile.ReadSettings(_filePath);

		Assert.NotNull(read);
		Assert.Equal(@"C:\Users\test\OneDrive\Zeiterfassung", read.DataFolder);
	}

	[Fact]
	public void WriteSettings_FolderDoesNotExistYet_CreatesIt()
	{
		string nestedPath = Path.Combine(_folder, "Zeiterfassung", "einstellungen.json");

		SettingsFile.WriteSettings(nestedPath, new AppSettings(@"C:\Daten"));

		Assert.True(File.Exists(nestedPath));
	}

	[Fact]
	public void WriteSettings_Always_LeavesNoTemporaryFileBehind()
	{
		SettingsFile.WriteSettings(_filePath, new AppSettings(@"C:\Daten"));

		Assert.Equal([_filePath], Directory.GetFiles(_folder));
	}

	[Fact]
	public void ReadSettings_WrongFormatVersion_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"formatVersion\": 3, \"dataFolder\": \"C:\\\\Daten\" }");

		DataFileException error = Assert.Throws<DataFileException>(() => SettingsFile.ReadSettings(_filePath));

		Assert.Contains("Formatversion 3", error.Message);
	}

	[Fact]
	public void ReadSettings_FormatVersionOneFromBeforeTheThemeSetting_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"formatVersion\": 1, \"dataFolder\": \"C:\\\\Daten\" }");

		DataFileException error = Assert.Throws<DataFileException>(() => SettingsFile.ReadSettings(_filePath));

		Assert.Contains("Formatversion 1", error.Message);
	}

	[Fact]
	public void ReadSettings_MissingFormatVersion_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"dataFolder\": \"C:\\\\Daten\" }");

		Assert.Throws<DataFileException>(() => SettingsFile.ReadSettings(_filePath));
	}

	[Fact]
	public void ReadSettings_MissingDataFolder_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"formatVersion\": 2 }");

		Assert.Throws<DataFileException>(() => SettingsFile.ReadSettings(_filePath));
	}

	[Fact]
	public void ReadSettings_BrokenJson_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"formatVersion\": 2, ");

		Assert.Throws<DataFileException>(() => SettingsFile.ReadSettings(_filePath));
	}

	[Fact]
	public void ReadSettings_UnknownField_ThrowsDataFileException()
	{
		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 2, \"dataFolder\": \"C:\\\\Daten\", \"schriftgroesse\": 12 }");

		Assert.Throws<DataFileException>(() => SettingsFile.ReadSettings(_filePath));
	}

	[Fact]
	public void WriteAndReadSettings_ChosenTheme_KeepsTheChoice()
	{
		SettingsFile.WriteSettings(_filePath, new AppSettings(@"C:\Daten", ThemePreference.Dark));

		AppSettings? read = SettingsFile.ReadSettings(_filePath);

		Assert.NotNull(read);
		Assert.Equal(ThemePreference.Dark, read.Theme);
	}

	[Fact]
	public void WriteSettings_WithoutAChosenTheme_WritesSystem()
	{
		SettingsFile.WriteSettings(_filePath, new AppSettings(@"C:\Daten"));

		Assert.Contains("\"theme\": \"system\"", File.ReadAllText(_filePath));
	}

	[Fact]
	public void ReadSettings_MissingTheme_FallsBackToSystem()
	{
		File.WriteAllText(_filePath, "{ \"formatVersion\": 2, \"dataFolder\": \"C:\\\\Daten\" }");

		AppSettings? read = SettingsFile.ReadSettings(_filePath);

		Assert.NotNull(read);
		Assert.Equal(ThemePreference.System, read.Theme);
	}

	[Fact]
	public void ReadSettings_UnknownTheme_ThrowsDataFileExceptionNamingTheAllowedValues()
	{
		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 2, \"dataFolder\": \"C:\\\\Daten\", \"theme\": \"dunkel\" }");

		DataFileException error = Assert.Throws<DataFileException>(() => SettingsFile.ReadSettings(_filePath));

		Assert.Contains("dunkel", error.Message);
		Assert.Contains("system, light und dark", error.Message);
	}

	[Fact]
	public void ReadSettings_ThemeInCapitalLetters_IsStillUnderstood()
	{
		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 2, \"dataFolder\": \"C:\\\\Daten\", \"theme\": \"LIGHT\" }");

		AppSettings? read = SettingsFile.ReadSettings(_filePath);

		Assert.NotNull(read);
		Assert.Equal(ThemePreference.Light, read.Theme);
	}

	[Fact]
	public void WithTheme_Always_KeepsTheDataFolder()
	{
		AppSettings original = new(@"C:\Daten", ThemePreference.System);

		AppSettings changed = original.WithTheme(ThemePreference.Light);

		Assert.Equal(@"C:\Daten", changed.DataFolder);
		Assert.Equal(ThemePreference.Light, changed.Theme);
		Assert.Equal(ThemePreference.System, original.Theme);
	}

	[Fact]
	public void ReadSettings_MissingFolderOnDisk_StillReturnsThePath()
	{
		string vanishedFolder = Path.Combine(_folder, "weg");
		SettingsFile.WriteSettings(_filePath, new AppSettings(vanishedFolder));

		AppSettings? read = SettingsFile.ReadSettings(_filePath);

		Assert.NotNull(read);
		Assert.Equal(vanishedFolder, read.DataFolder);
	}

	[Fact]
	public void GetDefaultFilePath_Always_PointsIntoLocalAppData()
	{
		string path = SettingsFile.GetDefaultFilePath();

		Assert.StartsWith(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			path);
		Assert.EndsWith(Path.Combine("Zeiterfassung", "einstellungen.json"), path);
	}
}
