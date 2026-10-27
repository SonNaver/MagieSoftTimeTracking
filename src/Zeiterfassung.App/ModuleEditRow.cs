namespace Zeiterfassung.App;

/// <summary>
/// A row in the module dialog, formatted ready for display.
/// </summary>
/// <remarks>
/// Besides the display text it carries the two facts the buttons' availability hangs on:
/// whether the module is still offered, and whether time has already run on it. Both are
/// worked out in the core; only the result stands here.
/// </remarks>
public sealed class ModuleEditRow
{
	/// <summary>
	/// Creates a row of the module dialog.
	/// </summary>
	/// <param name="name">The module name.</param>
	/// <param name="isActive">Whether the module is offered for tracking.</param>
	/// <param name="isUsed">Whether at least one entry runs on the module.</param>
	public ModuleEditRow(string name, bool isActive, bool isUsed)
	{
		Name = name;
		IsActive = isActive;
		IsUsed = isUsed;
	}

	/// <summary>
	/// The module name.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Whether the module is offered for tracking.
	/// </summary>
	public bool IsActive { get; }

	/// <summary>
	/// Whether at least one entry runs on the module.
	/// </summary>
	/// <remarks>
	/// A used module can be neither renamed nor deleted, because its name stands in plain text
	/// in every one of those entries.
	/// </remarks>
	public bool IsUsed { get; }

	/// <summary>
	/// The state of the module as text for the list.
	/// </summary>
	public string State => IsActive ? "steht zur Wahl" : "deaktiviert";

	/// <summary>
	/// Whether time has already been booked on the module, as text for the list.
	/// </summary>
	/// <remarks>
	/// Deliberately kept short: what follows from it — only deactivation remains — stands in
	/// the explanatory text above the list and in the tooltip of the disabled buttons. In the
	/// column it would be cut off and would say the same thing a third time.
	/// </remarks>
	public string Usage => IsUsed ? "Zeit gebucht" : "noch keine Zeit gebucht";
}
