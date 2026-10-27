using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Storage;

namespace Zeiterfassung.Core.Tests.Storage;

public sealed class ModuleFileTests : IDisposable
{
	private readonly string _folder;
	private readonly string _filePath;

	public ModuleFileTests()
	{
		_folder = Path.Combine(Path.GetTempPath(), "Zeiterfassung.Tests", Guid.NewGuid().ToString());
		Directory.CreateDirectory(_folder);
		_filePath = Path.Combine(_folder, "module.json");
	}

	/// <summary>
	/// Clears the test folder and its contents away again.
	/// </summary>
	public void Dispose()
	{
		Directory.Delete(_folder, recursive: true);
	}

	[Fact]
	public void ReadModules_SeveralModules_KeepsFileOrder()
	{
		WriteModuleFile("Statistik", "Mathematik II", "Betriebssysteme");

		StudyModuleList modules = ModuleFile.ReadModules(_filePath);

		Assert.Equal(
			["Statistik", "Mathematik II", "Betriebssysteme"],
			modules.Modules.Select(module => module.Name));
	}

	[Fact]
	public void ReadModules_NameWithSurroundingSpace_TrimsIt()
	{
		WriteModuleFile("  Mathematik II  ");

		StudyModuleList modules = ModuleFile.ReadModules(_filePath);

		Assert.Equal("Mathematik II", Assert.Single(modules.Modules).Name);
	}

	[Fact]
	public void ReadModules_DuplicateName_ThrowsDataFileExceptionNamingTheModule()
	{
		WriteModuleFile("Mathematik II", "Mathematik II");

		DataFileException error = Assert.Throws<DataFileException>(() => ModuleFile.ReadModules(_filePath));

		Assert.Contains("Mathematik II", error.Message);
	}

	[Fact]
	public void ReadModules_DuplicateName_MessageHasNoTechnicalParameterHint()
	{
		WriteModuleFile("Mathematik II", "Mathematik II");

		DataFileException error = Assert.Throws<DataFileException>(() => ModuleFile.ReadModules(_filePath));

		Assert.DoesNotContain("modules", error.Message);
	}

	[Fact]
	public void ReadModules_ModuleWithoutName_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"formatVersion\": 2, \"modules\": [ { } ] }");

		Assert.Throws<DataFileException>(() => ModuleFile.ReadModules(_filePath));
	}

	[Fact]
	public void ReadModules_BlankName_ThrowsDataFileException()
	{
		WriteModuleFile("   ");

		Assert.Throws<DataFileException>(() => ModuleFile.ReadModules(_filePath));
	}

	[Fact]
	public void ReadModules_MissingFile_ThrowsDataFileException()
	{
		DataFileException error = Assert.Throws<DataFileException>(() => ModuleFile.ReadModules(_filePath));

		Assert.Contains("nicht gefunden", error.Message);
	}

	[Fact]
	public void ReadModules_WrongFormatVersion_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"formatVersion\": 3, \"modules\": [] }");

		DataFileException error = Assert.Throws<DataFileException>(() => ModuleFile.ReadModules(_filePath));

		Assert.Contains("Formatversion 3", error.Message);
	}

	[Fact]
	public void ReadModules_MissingFormatVersion_ThrowsDataFileException()
	{
		File.WriteAllText(_filePath, "{ \"modules\": [] }");

		Assert.Throws<DataFileException>(() => ModuleFile.ReadModules(_filePath));
	}

	[Fact]
	public void ReadModules_UnknownFieldOnModule_ThrowsDataFileException()
	{
		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 2, \"modules\": [ { \"name\": \"Mathematik II\", \"shortName\": \"MA2\" } ] }");

		Assert.Throws<DataFileException>(() => ModuleFile.ReadModules(_filePath));
	}

	[Fact]
	public void CreateEmptyFile_NewFile_CanBeReadAndIsEmpty()
	{
		ModuleFile.CreateEmptyFile(_filePath);

		Assert.True(ModuleFile.ReadModules(_filePath).IsEmpty);
	}

	[Fact]
	public void CreateEmptyFile_NewFile_ContainsNoSampleModule()
	{
		ModuleFile.CreateEmptyFile(_filePath);

		Assert.Empty(ModuleFile.ReadModules(_filePath).Modules);
	}

	[Fact]
	public void CreateEmptyFile_Always_LeavesNoTemporaryFileBehind()
	{
		ModuleFile.CreateEmptyFile(_filePath);

		Assert.Equal([_filePath], Directory.GetFiles(_folder));
	}

	[Fact]
	public void ReadModules_ModuleWithoutIsActive_CountsAsActive()
	{
		WriteModuleFile("Mathematik II");

		StudyModuleList modules = ModuleFile.ReadModules(_filePath);

		Assert.True(modules.Modules[0].IsActive);
	}

	[Fact]
	public void ReadModules_DeactivatedModule_IsReadAsInactive()
	{
		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 2, \"modules\": [ { \"name\": \"Alt\", \"isActive\": false } ] }");

		StudyModuleList modules = ModuleFile.ReadModules(_filePath);

		Assert.False(modules.Modules[0].IsActive);
	}

	[Fact]
	public void WriteAndReadModules_MixedModules_KeepsNamesOrderAndState()
	{
		StudyModuleList written = new(
		[
			new StudyModule("Mathematik II"),
			new StudyModule("Betriebssysteme", isActive: false),
			new StudyModule("Datenbanken"),
		]);

		ModuleFile.WriteModules(_filePath, written);
		StudyModuleList read = ModuleFile.ReadModules(_filePath);

		Assert.Equal(["Mathematik II", "Betriebssysteme", "Datenbanken"], read.Modules.Select(module => module.Name));
		Assert.Equal([true, false, true], read.Modules.Select(module => module.IsActive));
	}

	[Fact]
	public void WriteModules_Always_LeavesNoTemporaryFileBehind()
	{
		ModuleFile.WriteModules(_filePath, new StudyModuleList([new StudyModule("Mathematik II")]));

		Assert.Equal([_filePath], Directory.GetFiles(_folder));
	}

	[Fact]
	public void ActiveModules_SomeDeactivated_ReturnsOnlyTheActiveOnes()
	{
		StudyModuleList modules = new(
		[
			new StudyModule("Mathematik II"),
			new StudyModule("Betriebssysteme", isActive: false),
		]);

		Assert.Equal(["Mathematik II"], modules.ActiveModules.Select(module => module.Name));
	}

	[Fact]
	public void ActiveModules_AllDeactivated_IsEmptyButListIsNot()
	{
		StudyModuleList modules = new([new StudyModule("Alt", isActive: false)]);

		Assert.Empty(modules.ActiveModules);
		Assert.False(modules.IsEmpty);
	}

	/// <summary>
	/// Writes a module file with the given names as raw text.
	/// </summary>
	/// <param name="names">The module names, exactly as they are to stand in the file.</param>
	private void WriteModuleFile(params string[] names)
	{
		IEnumerable<string> modules = names.Select(name => $"{{ \"name\": \"{name}\" }}");

		File.WriteAllText(
			_filePath,
			"{ \"formatVersion\": 2, \"modules\": [ " + string.Join(", ", modules) + " ] }");
	}
}
