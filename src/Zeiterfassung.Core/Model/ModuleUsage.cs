namespace Zeiterfassung.Core.Model;

/// <summary>
/// Answers what can still be changed about a module and what cannot.
/// </summary>
/// <remarks>
/// The rule hangs on a hard constraint of the data format: every time entry stores the module
/// name <em>in plain text</em> and not as a reference into the module list. Were a used module
/// renamed or deleted, every old entry would still hang on the old name and appear in the
/// report as an unknown module. A used module can therefore only be deactivated.
/// </remarks>
public static class ModuleUsage
{
	/// <summary>
	/// Collects the names of all modules that at least one entry runs on.
	/// </summary>
	/// <remarks>
	/// The open entry deliberately counts. It already sits on disk and carries its module name;
	/// that it is still missing its end changes nothing about that.
	/// </remarks>
	/// <param name="entries">The time entries that were read.</param>
	/// <returns>The used module names, ignoring case.</returns>
	public static IReadOnlySet<string> CollectUsedModuleNames(IEnumerable<TimeEntry> entries)
	{
		HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);

		foreach (TimeEntry entry in entries)
		{
			usedNames.Add(entry.Module);
		}

		return usedNames;
	}

	/// <summary>
	/// States whether a module may still be freely renamed and deleted.
	/// </summary>
	/// <remarks>
	/// The comparison ignores case, as everywhere module names are compared. Two names that
	/// differ only in case are the same module to the user.
	/// </remarks>
	/// <param name="moduleName">The module name to check.</param>
	/// <param name="usedModuleNames">The used module names from <see cref="CollectUsedModuleNames"/>.</param>
	/// <returns>True when not a single entry runs on this module.</returns>
	public static bool CanBeEdited(string moduleName, IReadOnlySet<string> usedModuleNames)
	{
		return !usedModuleNames.Contains(moduleName);
	}
}
