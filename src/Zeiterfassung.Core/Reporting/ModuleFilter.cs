using Zeiterfassung.Core.Model;

namespace Zeiterfassung.Core.Reporting;

/// <summary>
/// A selection of modules that a report is narrowed down to.
/// </summary>
/// <remarks>
/// An empty selection is not „no module“ but „no narrowing“: it lets every entry pass. That
/// way the state of an untouched filter and the state of a filter with all boxes cleared are
/// the same, and nobody ends up looking at an empty report by accident. Names are compared
/// ignoring case, as everywhere else in the report.
/// </remarks>
public sealed class ModuleFilter
{
	private readonly HashSet<string> _modules;

	/// <summary>
	/// Creates a filter from a selection of module names.
	/// </summary>
	/// <param name="modules">The selected module names; empty for no narrowing.</param>
	public ModuleFilter(IEnumerable<string> modules)
	{
		ArgumentNullException.ThrowIfNull(modules);

		_modules = new HashSet<string>(modules, StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// The filter that lets every entry pass.
	/// </summary>
	public static ModuleFilter All { get; } = new ModuleFilter([]);

	/// <summary>
	/// States whether the filter narrows anything down at all.
	/// </summary>
	public bool IsActive => _modules.Count > 0;

	/// <summary>
	/// Checks whether an entry passes the filter.
	/// </summary>
	/// <param name="entry">The entry to check.</param>
	/// <returns>True when the filter is inactive or the entry's module is selected.</returns>
	public bool Matches(TimeEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);

		return !IsActive || _modules.Contains(entry.Module);
	}
}
