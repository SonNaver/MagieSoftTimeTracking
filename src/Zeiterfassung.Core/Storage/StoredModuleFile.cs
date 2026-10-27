namespace Zeiterfassung.Core.Storage;

/// <summary>
/// The image of <c>module.json</c>, field by field as it stands in the file.
/// </summary>
internal sealed class StoredModuleFile
{
	/// <summary>
	/// The format version of the file.
	/// </summary>
	public int? FormatVersion { get; set; }

	/// <summary>
	/// The modules in the order of the file.
	/// </summary>
	public List<StoredModule>? Modules { get; set; }
}
