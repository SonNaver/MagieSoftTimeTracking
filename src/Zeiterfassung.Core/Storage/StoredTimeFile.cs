namespace Zeiterfassung.Core.Storage;

/// <summary>
/// The image of <c>zeiten.json</c>, field by field as it stands in the file.
/// </summary>
/// <remarks>
/// Deliberately separate from the data model: the serializer needs freely settable properties,
/// which the model intentionally does not have. Besides, the duration stands here as a field,
/// whereas the model computes it.
/// </remarks>
internal sealed class StoredTimeFile
{
	/// <summary>
	/// The format version of the file.
	/// </summary>
	/// <remarks>
	/// An <c>int?</c>, so that a missing entry stays distinguishable from version 0. Both are
	/// rejected, but with different messages.
	/// </remarks>
	public int? FormatVersion { get; set; }

	/// <summary>
	/// The entries in the order of the file.
	/// </summary>
	public List<StoredTimeEntry>? Entries { get; set; }
}
