using System.IO;
using System.Windows;
using System.Windows.Threading;
using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Storage;

namespace Zeiterfassung.App;

/// <summary>
/// The entry point of the application.
/// </summary>
public partial class App : Application
{
	private SingleInstanceGuard? _instanceGuard;

	/// <summary>
	/// Turns a second start away and otherwise sets up the handling of unexpected errors.
	/// </summary>
	/// <remarks>
	/// The main window is created here deliberately and not through <c>StartupUri</c>. WPF
	/// would create the window from <c>StartupUri</c> only after this event, and without regard
	/// to the fact that we are just shutting down — it would then flash up briefly. A window
	/// comes into being at all only in the case of the first instance.
	/// </remarks>
	/// <remarks>
	/// The colour scheme is applied before the first window comes into being. Otherwise WPF
	/// would draw the window light first and swap the colours a blink of an eye later.
	/// </remarks>
	/// <param name="eventArguments">The event data of the start.</param>
	protected override void OnStartup(StartupEventArgs eventArguments)
	{
		base.OnStartup(eventArguments);

		_instanceGuard = new SingleInstanceGuard();

		if (!_instanceGuard.IsOwner)
		{
			SingleInstanceGuard.ActivateRunningInstance();
			Shutdown();

			return;
		}

		DispatcherUnhandledException += HandleUnhandledException;

		AppearanceSetup.ApplyTheme(ReadStoredTheme());

		new MainWindow().Show();
	}

	/// <summary>
	/// Reads the stored appearance before the first window comes into being.
	/// </summary>
	/// <remarks>
	/// An error in the settings file is <em>not</em> reported here but quietly turns into the
	/// Windows appearance. That is no breach of the rule that errors get reported: the same
	/// file is read again by the main window moments later, and there the error appears
	/// properly in the message area. Reporting it a second time here, before any window
	/// stands, would bring up nothing but a message box — precisely what OBERFLAECHE.md,
	/// section 5 sets out to avoid.
	/// </remarks>
	/// <returns>The stored appearance, or the Windows one when none can be read.</returns>
	private static ThemePreference ReadStoredTheme()
	{
		try
		{
			return SettingsFile.ReadSettings(SettingsFile.GetDefaultFilePath())?.Theme
				?? ThemePreference.System;
		}
		catch (DataFileException)
		{
			return ThemePreference.System;
		}
	}

	/// <summary>
	/// Releases the single-instance lock again on shutdown.
	/// </summary>
	/// <param name="eventArguments">The event data of the exit.</param>
	protected override void OnExit(ExitEventArgs eventArguments)
	{
		_instanceGuard?.Dispose();
		_instanceGuard = null;

		base.OnExit(eventArguments);
	}

	/// <summary>
	/// Shows an unexpected error instead of letting the app die without a word.
	/// </summary>
	/// <param name="sender">The dispatcher raising the event.</param>
	/// <param name="eventArguments">The event data holding the exception.</param>
	private void HandleUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArguments)
	{
		File.WriteAllText(
			Path.Combine(Path.GetTempPath(), "zeiterfassung-fehler.txt"),
			eventArguments.Exception.ToString());

		MessageBox.Show(
			eventArguments.Exception.ToString(),
			"Unerwarteter Fehler",
			MessageBoxButton.OK,
			MessageBoxImage.Error);

		eventArguments.Handled = true;
	}
}
