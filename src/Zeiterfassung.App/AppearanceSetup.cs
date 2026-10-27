using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Zeiterfassung.Core.Model;

namespace Zeiterfassung.App;

/// <summary>
/// Puts the app's appearance onto the chosen setting.
/// </summary>
/// <remarks>
/// The choice stands in <c>einstellungen.json</c> and knows three values: follow the Windows
/// setting, always light, or always dark. With <em>System</em> the app follows when
/// Windows is switched while it runs; with the two fixed values, deliberately not.
/// </remarks>
internal static class AppearanceSetup
{
	/// <summary>
	/// The registry path under which Windows keeps the light/dark setting.
	/// </summary>
	private const string PersonalizeKeyPath =
		@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

	/// <summary>
	/// The value stating whether applications are to be drawn light.
	/// </summary>
	/// <remarks>
	/// Windows keeps two separate values: <c>SystemUsesLightTheme</c> for the taskbar and the
	/// start menu, <c>AppsUseLightTheme</c> for applications. The second one is decisive;
	/// whoever switches only the app mode in the Windows settings changes only that one.
	/// </remarks>
	private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

	/// <summary>
	/// The windows that are to listen for a switch by Windows.
	/// </summary>
	/// <remarks>
	/// Windows sends the notification through the window procedure, so a registration is needed
	/// per window. Because the choice can change while the app runs, the app must know which
	/// windows are currently open — otherwise the watching could not be turned off again.
	/// </remarks>
	private static readonly List<Window> WatchedWindows = [];

	/// <summary>
	/// The windows whose watching is currently actually running.
	/// </summary>
	/// <remarks>
	/// Kept apart from <see cref="WatchedWindows"/>, because the library answers the
	/// unregistering of a never-registered window with an exception — and a window registered
	/// while light or dark is fixed was never watched. Without this distinction the app crashed
	/// on startup as soon as a fixed choice had been saved.
	/// </remarks>
	private static readonly HashSet<Window> ActivelyWatched = [];

	/// <summary>
	/// The user's most recently applied choice.
	/// </summary>
	private static ThemePreference _preference = ThemePreference.System;

	/// <summary>
	/// The appearance that currently applies.
	/// </summary>
	/// <remarks>
	/// The choice is read on startup, before any window stands. The main window needs it
	/// afterwards to put the tick in the right place in its menu, and asks here instead of
	/// reading the settings file a second time.
	/// </remarks>
	internal static ThemePreference CurrentPreference => _preference;

	/// <summary>
	/// Puts the app onto the given appearance and sets the watching up to match.
	/// </summary>
	/// <remarks>
	/// Called on startup, before the first window comes into being, and again on every change
	/// of the choice. With <em>System</em> all open windows watch the Windows setting; with a
	/// fixed choice the watching is turned off, because it would otherwise overwrite the choice
	/// the next time Windows switches.
	/// </remarks>
	/// <param name="preference">The wanted appearance.</param>
	internal static void ApplyTheme(ThemePreference preference)
	{
		_preference = preference;

		ApplicationThemeManager.Apply(ResolveTheme(preference), WindowBackdropType.Mica, true);

		foreach (Window window in WatchedWindows)
		{
			UpdateWatching(window);
		}
	}

	/// <summary>
	/// Registers a window so that it can follow a switch by Windows.
	/// </summary>
	/// <remarks>
	/// The window unregisters itself again when it closes. Without that the list would hold on
	/// to closed windows, and a later change of the choice would run into a window that no
	/// longer exists.
	/// </remarks>
	/// <param name="window">The window to register.</param>
	internal static void RegisterWindow(Window window)
	{
		if (WatchedWindows.Contains(window))
		{
			return;
		}

		WatchedWindows.Add(window);
		window.Closed += HandleWindowClosed;

		UpdateWatching(window);
	}

	/// <summary>
	/// Takes a closed window back out of the list.
	/// </summary>
	/// <param name="sender">The closed window.</param>
	/// <param name="eventArguments">The event data.</param>
	private static void HandleWindowClosed(object? sender, EventArgs eventArguments)
	{
		if (sender is not Window window)
		{
			return;
		}

		window.Closed -= HandleWindowClosed;
		WatchedWindows.Remove(window);
		ActivelyWatched.Remove(window);
	}

	/// <summary>
	/// Turns the watching of a window on or off to match the current choice.
	/// </summary>
	/// <param name="window">The window concerned.</param>
	private static void UpdateWatching(Window window)
	{
		if (_preference == ThemePreference.System)
		{
			if (ActivelyWatched.Add(window))
			{
				SystemThemeWatcher.Watch(window, WindowBackdropType.Mica, true);
			}

			return;
		}

		if (ActivelyWatched.Remove(window))
		{
			SystemThemeWatcher.UnWatch(window);
		}
	}

	/// <summary>
	/// Translates the user's choice into the colour scheme actually to be applied.
	/// </summary>
	/// <param name="preference">The user's choice.</param>
	/// <returns>The colour scheme to apply.</returns>
	private static ApplicationTheme ResolveTheme(ThemePreference preference)
	{
		return preference switch
		{
			ThemePreference.Light => ApplicationTheme.Light,
			ThemePreference.Dark => ApplicationTheme.Dark,
			_ => ReadSystemTheme(),
		};
	}

	/// <summary>
	/// Reads from the registry whether Windows is currently set to light or dark.
	/// </summary>
	/// <remarks>
	/// If the value is missing or the key cannot be opened, light applies. That is the default
	/// of Windows itself and the state in which the app would be readable even without any
	/// adjustment.
	/// </remarks>
	/// <returns>The colour scheme to apply.</returns>
	private static ApplicationTheme ReadSystemTheme()
	{
		using RegistryKey? personalizeKey = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);

		if (personalizeKey?.GetValue(AppsUseLightThemeValueName) is not int usesLightTheme)
		{
			return ApplicationTheme.Light;
		}

		return usesLightTheme == 0 ? ApplicationTheme.Dark : ApplicationTheme.Light;
	}
}
