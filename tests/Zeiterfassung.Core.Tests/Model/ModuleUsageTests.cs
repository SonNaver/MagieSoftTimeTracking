using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Tests.Model;

public sealed class ModuleUsageTests
{
	[Fact]
	public void CollectUsedModuleNames_NoEntries_ReturnsNothing()
	{
		Assert.Empty(ModuleUsage.CollectUsedModuleNames([]));
	}

	[Fact]
	public void CollectUsedModuleNames_SeveralEntriesOnOneModule_ReturnsTheNameOnce()
	{
		IReadOnlySet<string> used = ModuleUsage.CollectUsedModuleNames(
		[
			CreateEntry("Mathematik II"),
			CreateEntry("Mathematik II"),
		]);

		Assert.Equal(["Mathematik II"], used);
	}

	[Fact]
	public void CollectUsedModuleNames_OpenEntry_CountsAsUsed()
	{
		TimeEntry openEntry = TimeEntry.StartEntry(DateTimeOffset.Now, "Betriebssysteme", null);

		Assert.Contains("Betriebssysteme", ModuleUsage.CollectUsedModuleNames([openEntry]));
	}

	[Fact]
	public void CanBeEdited_ModuleWithoutAnyEntry_IsTrue()
	{
		IReadOnlySet<string> used = ModuleUsage.CollectUsedModuleNames([CreateEntry("Mathematik II")]);

		Assert.True(ModuleUsage.CanBeEdited("Datenbanken", used));
	}

	[Fact]
	public void CanBeEdited_ModuleWithAnEntry_IsFalse()
	{
		IReadOnlySet<string> used = ModuleUsage.CollectUsedModuleNames([CreateEntry("Mathematik II")]);

		Assert.False(ModuleUsage.CanBeEdited("Mathematik II", used));
	}

	[Fact]
	public void CanBeEdited_NameDiffersOnlyInCasing_IsStillFalse()
	{
		IReadOnlySet<string> used = ModuleUsage.CollectUsedModuleNames([CreateEntry("Mathematik II")]);

		Assert.False(ModuleUsage.CanBeEdited("MATHEMATIK II", used));
	}

	/// <summary>
	/// Creates a completed entry on the given module.
	/// </summary>
	/// <param name="moduleName">The module name of the entry.</param>
	/// <returns>An entry with a start and an end.</returns>
	private static TimeEntry CreateEntry(string moduleName)
	{
		DateTimeOffset start = new(2026, 9, 24, 14, 0, 0, TimeSpan.FromHours(2));

		return TimeEntry.RestoreEntry(Guid.NewGuid(), start, start.AddHours(1), moduleName, null);
	}
}
