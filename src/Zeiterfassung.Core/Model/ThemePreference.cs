namespace Zeiterfassung.Core.Model;

/// <summary>
/// The user's choice of how light or dark the app is drawn.
/// </summary>
/// <remarks>
/// Lives in the core and not in the shell, because the choice ends up in
/// <c>einstellungen.json</c>, and reading and writing that file is the core's business. How the
/// colours are actually applied is known to the shell alone.
/// </remarks>
public enum ThemePreference
{
	/// <summary>
	/// The Windows app mode applies, and the app follows when it is switched.
	/// </summary>
	System,

	/// <summary>
	/// The app is always light, regardless of Windows.
	/// </summary>
	Light,

	/// <summary>
	/// The app is always dark, regardless of Windows.
	/// </summary>
	Dark,
}
