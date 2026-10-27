using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Tests.Model;

public sealed class StudyModuleTests
{
	[Fact]
	public void Constructor_PlainName_KeepsItUnchanged()
	{
		StudyModule module = new("Mathematik II");

		Assert.Equal("Mathematik II", module.Name);
	}

	[Fact]
	public void Constructor_NameWithSurroundingSpace_TrimsIt()
	{
		StudyModule module = new("  Mathematik II  ");

		Assert.Equal("Mathematik II", module.Name);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Constructor_BlankName_ThrowsArgumentException(string name)
	{
		Assert.Throws<ArgumentException>(() => new StudyModule(name));
	}
}
