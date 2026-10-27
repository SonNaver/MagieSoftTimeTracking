namespace Zeiterfassung.Core.Model;

/// <summary>
/// The list of study modules, as it stands in the module file.
/// </summary>
/// <remarks>
/// A class of its own and not a mere enumeration, because a rule hangs on the list as a whole:
/// no name may appear twice. Here that rule lives independently of where the modules come
/// from, and it can be checked without touching a file.
/// </remarks>
public sealed class StudyModuleList
{
	/// <summary>
	/// Creates a module list from the given modules.
	/// </summary>
	/// <remarks>
	/// The order is preserved. The user maintains the file by hand and expects the modules in
	/// the selection the way they wrote them down — so nothing is sorted.
	/// </remarks>
	/// <param name="modules">The modules in the order of the file.</param>
	/// <exception cref="ArgumentException">When two modules carry the same name.</exception>
	public StudyModuleList(IEnumerable<StudyModule> modules)
	{
		Modules = RequireDistinctNames(modules);
	}

	/// <summary>
	/// The modules in the order in which they stand in the file.
	/// </summary>
	public IReadOnlyList<StudyModule> Modules { get; }

	/// <summary>
	/// The modules that are offered for tracking.
	/// </summary>
	/// <remarks>
	/// Only these fill the selection list of the main window. The report, by contrast, reaches
	/// for <see cref="Modules"/>, because a deactivated module still has time running on it and
	/// must appear there.
	/// </remarks>
	public IReadOnlyList<StudyModule> ActiveModules =>
		[.. Modules.Where(module => module.IsActive)];

	/// <summary>
	/// States whether the list holds no module at all.
	/// </summary>
	/// <remarks>
	/// The normal case on the first start: the app has just created <c>module.json</c> as an
	/// empty template. The shell then reports „Kein Modul wählbar“ without that being an error
	/// (see OBERFLAECHE.md, section 5).
	/// </remarks>
	public bool IsEmpty => Modules.Count == 0;

	/// <summary>
	/// Checks that no module name appears twice and returns the modules as a fixed list.
	/// </summary>
	/// <remarks>
	/// The comparison ignores case. Two modules that differ only in case are the same to the
	/// user — the selection would show seemingly the same entry twice, and in the time data it
	/// would afterwards no longer be apparent which one was meant.
	/// </remarks>
	/// <param name="modules">The modules to check.</param>
	/// <returns>The modules as a fixed list in unchanged order.</returns>
	/// <exception cref="ArgumentException">When two modules carry the same name.</exception>
	private static IReadOnlyList<StudyModule> RequireDistinctNames(IEnumerable<StudyModule> modules)
	{
		List<StudyModule> checkedModules = [];
		HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);

		foreach (StudyModule module in modules)
		{
			if (!seenNames.Add(module.Name))
			{
				throw new ArgumentException(
					$"Das Modul „{module.Name}“ kommt mehr als einmal vor.",
					nameof(modules));
			}

			checkedModules.Add(module);
		}

		return checkedModules;
	}
}
