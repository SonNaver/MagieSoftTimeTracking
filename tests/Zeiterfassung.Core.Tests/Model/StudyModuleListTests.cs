using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Tests.Model;

public sealed class StudyModuleListTests
{
	[Fact]
	public void Constructor_SeveralModules_KeepsFileOrder()
	{
		StudyModuleList list = new([
			new StudyModule("Statistik"),
			new StudyModule("Mathematik II"),
			new StudyModule("Betriebssysteme")]);

		Assert.Equal(
			["Statistik", "Mathematik II", "Betriebssysteme"],
			list.Modules.Select(module => module.Name));
	}

	[Fact]
	public void Constructor_NoModules_YieldsEmptyList()
	{
		StudyModuleList list = new([]);

		Assert.True(list.IsEmpty);
		Assert.Empty(list.Modules);
	}

	[Fact]
	public void Constructor_OneModule_IsNotEmpty()
	{
		StudyModuleList list = new([new StudyModule("Mathematik II")]);

		Assert.False(list.IsEmpty);
	}

	[Fact]
	public void Constructor_DuplicateName_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() => new StudyModuleList([
			new StudyModule("Mathematik II"),
			new StudyModule("Mathematik II")]));
	}

	[Fact]
	public void Constructor_NameDifferingOnlyInCase_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() => new StudyModuleList([
			new StudyModule("Mathematik II"),
			new StudyModule("mathematik ii")]));
	}

	[Fact]
	public void Constructor_DuplicateAfterTrimming_ThrowsArgumentException()
	{
		Assert.Throws<ArgumentException>(() => new StudyModuleList([
			new StudyModule("Mathematik II"),
			new StudyModule("  Mathematik II  ")]));
	}

	[Fact]
	public void Constructor_DuplicateName_NamesTheModuleInTheMessage()
	{
		ArgumentException error = Assert.Throws<ArgumentException>(() => new StudyModuleList([
			new StudyModule("Mathematik II"),
			new StudyModule("Mathematik II")]));

		Assert.Contains("Mathematik II", error.Message);
	}
}
