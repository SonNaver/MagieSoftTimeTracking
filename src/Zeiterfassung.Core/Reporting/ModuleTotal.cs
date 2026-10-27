namespace Zeiterfassung.Core.Reporting;

/// <summary>
/// The total time spent on one module.
/// </summary>
public sealed class ModuleTotal
{
	/// <summary>
	/// Creates a module total.
	/// </summary>
	/// <param name="module">The module name in plain text.</param>
	/// <param name="total">The summed time of all completed entries.</param>
	/// <param name="entryCount">The number of entries the sum comes from.</param>
	/// <param name="isKnownModule">Whether the module still stands in the module list.</param>
	public ModuleTotal(string module, TimeSpan total, int entryCount, bool isKnownModule)
	{
		Module = module;
		Total = total;
		EntryCount = entryCount;
		IsKnownModule = isKnownModule;
	}

	/// <summary>
	/// The module name in plain text.
	/// </summary>
	/// <remarks>
	/// If the module still stands in <c>module.json</c>, this is that file's spelling;
	/// otherwise the one of the most recent entry. Grouping ignores case, because the module
	/// list forbids two names differing only in case anyway (DATENFORMAT.md, section 3).
	/// </remarks>
	public string Module { get; }

	/// <summary>
	/// The summed time of all completed entries of this module.
	/// </summary>
	public TimeSpan Total { get; }

	/// <summary>
	/// The number of entries the sum comes from.
	/// </summary>
	public int EntryCount { get; }

	/// <summary>
	/// States whether the module still stands in the module list.
	/// </summary>
	/// <remarks>
	/// If it does not, it was struck from <c>module.json</c> or mistyped there. The time stays
	/// in the sum all the same — done is done — but is marked in the user interface, so that a
	/// typo catches the eye instead of quietly producing a second row.
	/// </remarks>
	public bool IsKnownModule { get; }
}
