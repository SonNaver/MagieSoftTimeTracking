namespace Zeiterfassung.Core.Storage;

/// <summary>
/// The image of a single entry in <c>zeiten.json</c>.
/// </summary>
internal sealed class StoredTimeEntry
{
	/// <summary>
	/// The identifier of the entry.
	/// </summary>
	public Guid? Id { get; set; }

	/// <summary>
	/// The start.
	/// </summary>
	public DateTimeOffset? Start { get; set; }

	/// <summary>
	/// The end, or null for an open entry.
	/// </summary>
	public DateTimeOffset? End { get; set; }

	/// <summary>
	/// The module name in plain text.
	/// </summary>
	public string? Module { get; set; }

	/// <summary>
	/// The activity as free text.
	/// </summary>
	public string? Note { get; set; }

	/// <summary>
	/// The stored duration in the pattern hours:minutes:seconds.
	/// </summary>
	/// <remarks>
	/// Filled when writing and **discarded** when reading — start and end are always
	/// authoritative (DATENFORMAT.md, section 2). The property exists all the same because
	/// unknown fields are rejected: without it, the app's own correctly written file would fail
	/// on the next read.
	/// </remarks>
	public string? Duration { get; set; }
}
