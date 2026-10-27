namespace Zeiterfassung.Core.Model;

/// <summary>
/// The local settings of this machine.
/// </summary>
/// <remarks>
/// Holds two things: the folder in which the two travelling data files live, and the choice
/// between light and dark appearance. Both are deliberately <em>not</em> carried along — the
/// folder sits at different paths on the desktop and the laptop, and the appearance
/// may differ from machine to machine.
/// </remarks>
public sealed class AppSettings
{
	/// <summary>
	/// Creates the settings with the given data folder and the given appearance.
	/// </summary>
	/// <param name="dataFolder">The folder in which the data files live.</param>
	/// <param name="theme">The wanted appearance; without one, the Windows setting applies.</param>
	/// <exception cref="ArgumentException">When the folder path is empty or only whitespace.</exception>
	public AppSettings(string dataFolder, ThemePreference theme = ThemePreference.System)
	{
		if (string.IsNullOrWhiteSpace(dataFolder))
		{
			throw new ArgumentException("Der Datenordner ist nicht angegeben.", nameof(dataFolder));
		}

		DataFolder = dataFolder.Trim();
		Theme = theme;
	}

	/// <summary>
	/// The folder in which <c>zeiten.json</c> and <c>module.json</c> live on this machine.
	/// </summary>
	public string DataFolder { get; }

	/// <summary>
	/// The chosen appearance: follow the Windows setting, always light, or always dark.
	/// </summary>
	public ThemePreference Theme { get; }

	/// <summary>
	/// Returns the same settings with a different appearance.
	/// </summary>
	/// <remarks>
	/// The settings are immutable so that nobody passes them on half written by accident.
	/// Changing the appearance therefore yields a new instance.
	/// </remarks>
	/// <param name="theme">The new appearance.</param>
	/// <returns>New settings with an unchanged data folder.</returns>
	public AppSettings WithTheme(ThemePreference theme)
	{
		return new AppSettings(DataFolder, theme);
	}
}
