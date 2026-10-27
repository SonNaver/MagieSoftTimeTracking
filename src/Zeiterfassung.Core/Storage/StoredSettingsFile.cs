namespace Zeiterfassung.Core.Storage;

/// <summary>
/// The image of <c>einstellungen.json</c>, field by field as it stands in the file.
/// </summary>
internal sealed class StoredSettingsFile
{
	/// <summary>
	/// The format version of the file.
	/// </summary>
	public int? FormatVersion { get; set; }

	/// <summary>
	/// The folder in which the two travelling data files live.
	/// </summary>
	public string? DataFolder { get; set; }

	/// <summary>
	/// The chosen appearance as text: <c>system</c>, <c>light</c> or <c>dark</c>.
	/// </summary>
	/// <remarks>
	/// Kept as text rather than an enumeration value, so that an unknown value gets its own
	/// understandable message instead of the one .NET produces. The file is repaired by hand,
	/// which makes a typo in this spot the likely case.
	/// </remarks>
	public string? Theme { get; set; }
}
