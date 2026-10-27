namespace Zeiterfassung.Core.Storage;

/// <summary>
/// The image of a single module in <c>module.json</c>.
/// </summary>
/// <remarks>
/// An object with a name field and not merely a string — that leaves room for later additions
/// such as an abbreviation or a sort order without reshaping the file. Such an extension does
/// cost a bump of the format version, though, because unknown fields are rejected.
/// </remarks>
internal sealed class StoredModule
{
	/// <summary>
	/// The module name.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Whether the module is offered for tracking.
	/// </summary>
	/// <remarks>
	/// May be absent; the module then counts as active. That keeps a hand-written file
	/// forgiving: adding a module should require writing nothing but the name.
	/// </remarks>
	public bool? IsActive { get; set; }
}
