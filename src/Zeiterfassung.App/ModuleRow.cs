namespace Zeiterfassung.App;

/// <summary>
/// A row of the module report, formatted ready for display.
/// </summary>
/// <remarks>
/// The shell computes nothing; it merely turns the core's totals into text. That is why
/// nothing but strings stands here, and no time values.
/// </remarks>
public sealed class ModuleRow
{
	/// <summary>
	/// Creates a row of the module report.
	/// </summary>
	/// <param name="module">The module name.</param>
	/// <param name="total">The total time as text.</param>
	/// <param name="entryCount">The number of entries.</param>
	/// <param name="hint">The hint about a module no longer listed, otherwise empty.</param>
	public ModuleRow(string module, string total, int entryCount, string hint)
	{
		Module = module;
		Total = total;
		EntryCount = entryCount;
		Hint = hint;
	}

	/// <summary>
	/// The module name.
	/// </summary>
	public string Module { get; }

	/// <summary>
	/// The total time in the pattern hours:minutes:seconds.
	/// </summary>
	public string Total { get; }

	/// <summary>
	/// The number of entries the total comes from.
	/// </summary>
	public int EntryCount { get; }

	/// <summary>
	/// The hint that the module no longer stands in the module list, otherwise empty.
	/// </summary>
	public string Hint { get; }
}
