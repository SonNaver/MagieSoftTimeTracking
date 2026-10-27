using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Storage;
using Zeiterfassung.Core.Workflow;

// The UI library brings its own types that are named exactly like those of WPF — Button,
// TextBox, MessageBox. A blanket "using" made every such spot ambiguous, so the two types
// actually needed are named one by one.
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Zeiterfassung.App;

/// <summary>
/// The main window: module selection, activity, play and stop.
/// </summary>
/// <remarks>
/// Deliberately holds no business logic. Everything that gets decided is decided by
/// the core; this window displays, takes clicks, and passes messages through.
/// </remarks>
public partial class MainWindow : FluentWindow
{
	/// <summary>
	/// The width of the window in the compact view.
	/// </summary>
	private const double CompactWindowWidth = 300;

	/// <summary>
	/// The greatest length of the module name in the title bar of the compact view.
	/// </summary>
	/// <remarks>
	/// The self-drawn title bar does not shorten a title that is too long but pushes the window
	/// buttons out of the window to the right — with a narrow window the close cross was
	/// thereby out of reach. The name is therefore shortened beforehand.
	/// </remarks>
	private const int CompactTitleMaxLength = 20;

	/// <summary>
	/// The font size of the time display in the full view.
	/// </summary>
	private const double FullClockFontSize = 46;

	/// <summary>
	/// The font size of the time display in the compact view.
	/// </summary>
	/// <remarks>
	/// Smaller than in the full view: in the narrow window the display is the only thing that
	/// needs room anyway, and at full size it would bump into the edges above and below.
	/// </remarks>
	private const double CompactClockFontSize = 36;

	/// <summary>
	/// The height of the window in the compact view.
	/// </summary>
	/// <remarks>
	/// Taller than it used to be: since the move to the UI library, the title bar is drawn by
	/// the window itself and thereby counts towards the height of the content.
	/// </remarks>
	private const double CompactWindowHeight = 210;

	/// <summary>
	/// The smallest width of the window in the full view.
	/// </summary>
	private const double FullMinWidth = 420;

	/// <summary>
	/// The smallest height of the window in the full view.
	/// </summary>
	/// <remarks>
	/// Must agree with <c>MinHeight</c> in MainWindow.xaml: there the value applies at startup,
	/// here on the way back from the compact view.
	/// </remarks>
	/// <remarks>
	/// The value is this large because the message area must show a multi-line message with a
	/// file path and the folder button in full (OBERFLAECHE.md, section 5). With a shorter
	/// window the box pushed itself over the buttons above it.
	/// </remarks>
	private const double FullMinHeight = 600;

	private readonly DispatcherTimer _elapsedTimer;
	private readonly string _settingsFilePath;

	private Workspace? _workspace;
	private TimeTracker? _tracker;
	private ReportWindow? _reportWindow;

	private bool _isCompactMode;
	private double _fullWidth;
	private double _fullHeight;

	/// <summary>
	/// Creates the main window and sets up the running display.
	/// </summary>
	public MainWindow()
	{
		InitializeComponent();

		AppearanceSetup.RegisterWindow(this);
		UpdateThemeMenu();

		_settingsFilePath = SettingsFile.GetDefaultFilePath();
		_elapsedTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1),
		};
		_elapsedTimer.Tick += HandleTimerTick;

		ContentRendered += HandleContentRendered;
	}

	/// <summary>
	/// Sets the app up as soon as the window has been drawn.
	/// </summary>
	/// <remarks>
	/// Hangs on <c>ContentRendered</c> and not on <c>Loaded</c>, and unsubscribes itself right
	/// away: a modal dialog is opened during startup, and for that the window should be fully
	/// drawn and the entry demonstrably taken only once.
	/// </remarks>
	/// <param name="sender">The window raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleContentRendered(object? sender, EventArgs eventArguments)
	{
		ContentRendered -= HandleContentRendered;

		OpenOrChooseWorkspace();
	}

	/// <summary>
	/// Opens the remembered data folder, or has one picked on the first start.
	/// </summary>
	private void OpenOrChooseWorkspace()
	{
		try
		{
			_workspace = Workspace.OpenWorkspace(_settingsFilePath);
		}
		catch (DataFileException error)
		{
			ShowProblem("Daten nicht lesbar", error.Message);

			return;
		}

		if (_workspace is null)
		{
			ChooseDataFolder();

			return;
		}

		PrepareTracking();
	}

	/// <summary>
	/// Lets the user pick the data folder and sets it up.
	/// </summary>
	/// <remarks>
	/// If the user cancels, the window stays put with a message and the folder button. No
	/// coercion and no shutting down — it is the same path as for a folder that later went
	/// missing, so one mechanism instead of two.
	/// </remarks>
	private void ChooseDataFolder()
	{
		OpenFolderDialog dialog = new()
		{
			Title = "Ordner für die Zeiterfassungs-Daten wählen",
			Multiselect = false,
		};

		if (dialog.ShowDialog(this) != true)
		{
			ShowProblem(
				"Kein Ordner gewählt",
				"Die Zeiterfassung braucht einen Ordner für ihre Daten — am besten einen im "
				+ "OneDrive-Ordner, damit die Erfassung zwischen deinen Rechnern wandert.");

			return;
		}

		try
		{
			// The chosen appearance is passed through because the settings file is rewritten in
			// full here; without it the app would fall back to the Windows appearance as soon
			// as the folder is picked again.
			_workspace = Workspace.CreateWorkspace(
				_settingsFilePath,
				dialog.FolderName,
				AppearanceSetup.CurrentPreference);
		}
		catch (DataFileException error)
		{
			ShowProblem("Ordner nicht verwendbar", error.Message);

			return;
		}

		PrepareTracking();
	}

	/// <summary>
	/// Loads the module list and deals with any open entry.
	/// </summary>
	private void PrepareTracking()
	{
		if (_workspace is null)
		{
			return;
		}

		_tracker = new TimeTracker(_workspace, TimeProvider.System);

		try
		{
			StudyModuleList modules = _workspace.ReadModules();

			// Only active modules are offered. A deactivated one stays in the file and keeps
			// counting in the report, but is not to be booked on any more.
			ModuleSelection.ItemsSource = modules.ActiveModules;

			if (modules.ActiveModules.Count == 0)
			{
				ShowProblem(
					modules.IsEmpty ? "Noch kein Modul eingetragen" : "Kein Modul steht zur Wahl",
					modules.IsEmpty
						? "Über den Knopf neben der Auswahl lässt sich das erste Modul anlegen. "
						+ "Wer lieber die Datei bearbeitet, findet sie hier:\n\n"
						+ $"{_workspace.ModuleFilePath}"
						: "Alle Module sind deaktiviert. Über den Knopf neben der Auswahl lässt "
						+ "sich eines wieder aufnehmen oder ein neues anlegen.",
					offerFolderChoice: false);

				return;
			}

			ModuleSelection.SelectedIndex = 0;
			ShowStatus($"Daten geladen aus „{_workspace.DataFolder}“.");
			ResolveOpenEntry();
		}
		catch (DataFileException error)
		{
			ShowProblem("Daten nicht lesbar", error.Message);
		}
	}

	/// <summary>
	/// Deals with an open entry found at startup.
	/// </summary>
	/// <remarks>
	/// Both ways — complete the entry or let it keep running — are always offered; the user makes the choice. If they close the
	/// dialog's window without deciding, the entry stays open and the question comes up again
	/// on the next start.
	/// </remarks>
	private void ResolveOpenEntry()
	{
		if (_tracker is null)
		{
			return;
		}

		TimeEntry? openEntry = _tracker.FindOpenEntry();

		if (openEntry is null)
		{
			UpdateControlState();

			return;
		}

		OpenEntryDialog dialog = new(openEntry)
		{
			Owner = this,
		};

		dialog.ShowDialog();

		switch (dialog.Choice)
		{
			case OpenEntryChoice.Resume:
				_tracker.ResumeTracking(openEntry);
				SelectModule(openEntry.Module);
				ActivityText.Text = openEntry.Note;
				_elapsedTimer.Start();
				ShowStatus("Die offene Sitzung läuft weiter.");
				break;

			case OpenEntryChoice.Complete:
				_tracker.CompleteOpenEntry(openEntry, dialog.ChosenEnd);
				ShowStatus("Die offene Sitzung wurde abgeschlossen.");
				break;

			default:
				ShowStatus("Die offene Sitzung bleibt offen.");
				break;
		}

		UpdateControlState();
	}

	/// <summary>
	/// Begins a session for the selected module.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandlePlayClick(object sender, RoutedEventArgs eventArguments)
	{
		if (_tracker is null || ModuleSelection.SelectedItem is not StudyModule selectedModule)
		{
			return;
		}

		try
		{
			_tracker.StartTracking(selectedModule.Name, ActivityText.Text);
			_elapsedTimer.Start();
			ShowElapsedTime();
			ShowStatus($"Läuft seit {DateTime.Now:HH:mm:ss} — {selectedModule.Name}.");
		}
		catch (DataFileException error)
		{
			ShowProblem("Start nicht möglich", error.Message);
		}

		UpdateControlState();
	}

	/// <summary>
	/// Ends the running session.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleStopClick(object sender, RoutedEventArgs eventArguments)
	{
		StopTracking();
	}

	/// <summary>
	/// Ends the running session and reports the result.
	/// </summary>
	/// <returns>True when the session could be ended.</returns>
	private bool StopTracking()
	{
		if (_tracker is null || !_tracker.IsTracking)
		{
			return true;
		}

		try
		{
			TimeEntry stopped = _tracker.StopTracking();

			_elapsedTimer.Stop();
			ElapsedDisplay.Text = DurationFormat.FormatDuration(stopped.Duration ?? TimeSpan.Zero);
			ShowStatus(
				$"Gespeichert: {stopped.Module}, "
				+ $"{DurationFormat.FormatDuration(stopped.Duration ?? TimeSpan.Zero)}.");
			UpdateControlState();

			return true;
		}
		catch (DataFileException error)
		{
			ShowProblem("Stopp nicht gespeichert", error.Message);
			UpdateControlState();

			return false;
		}
	}

	/// <summary>
	/// Lets the user pick a different data folder.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleChooseFolderClick(object sender, RoutedEventArgs eventArguments)
	{
		ChooseDataFolder();
	}

	/// <summary>
	/// Checks on every change of the module selection whether play may be available.
	/// </summary>
	/// <param name="sender">The selection list raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleModuleSelectionChanged(object sender, SelectionChangedEventArgs eventArguments)
	{
		UpdateControlState();
	}

	/// <summary>
	/// Carries the running time forward once a second.
	/// </summary>
	/// <param name="sender">The timer raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleTimerTick(object? sender, EventArgs eventArguments)
	{
		ShowElapsedTime();
	}

	/// <summary>
	/// Asks on closing when a session is still running.
	/// </summary>
	/// <remarks>
	/// Both answers make sense: usually the user wants to end the session — but sometimes
	/// precisely not, because they are moving to the other machine and want to keep counting
	/// there, which is what the open entry is kept for.
	/// </remarks>
	/// <param name="sender">The window raising the event.</param>
	/// <param name="eventArguments">The event data, through which the closing can be cancelled.</param>
	private void HandleWindowClosing(object? sender, CancelEventArgs eventArguments)
	{
		if (_tracker is null || !_tracker.IsTracking)
		{
			return;
		}

		MessageBoxResult answer = MessageBox.Show(
			this,
			"Es läuft noch eine Sitzung.\n\n"
			+ "Ja — Sitzung stoppen und beenden.\n"
			+ "Nein — Sitzung offen lassen und beenden; sie kann später abgeschlossen oder "
			+ "fortgesetzt werden, auch auf dem anderen Rechner.\n"
			+ "Abbrechen — doch nicht beenden.",
			"Zeiterfassung beenden",
			MessageBoxButton.YesNoCancel,
			MessageBoxImage.Question);

		switch (answer)
		{
			case MessageBoxResult.Yes:
				eventArguments.Cancel = !StopTracking();
				break;

			case MessageBoxResult.No:
				break;

			default:
				eventArguments.Cancel = true;
				break;
		}
	}

	/// <summary>
	/// Selects the module with the given name in the selection list.
	/// </summary>
	/// <remarks>
	/// If the name no longer stands in the module list, the selection stays empty. The entry
	/// itself keeps its module name in plain text and is thereby undamaged.
	/// </remarks>
	/// <param name="moduleName">The module name to select.</param>
	private void SelectModule(string moduleName)
	{
		if (ModuleSelection.ItemsSource is not IEnumerable<StudyModule> modules)
		{
			return;
		}

		ModuleSelection.SelectedItem = modules.FirstOrDefault(module => module.Name == moduleName);
	}

	/// <summary>
	/// Shows the time elapsed so far in the running session.
	/// </summary>
	private void ShowElapsedTime()
	{
		if (_tracker is null || !_tracker.IsTracking)
		{
			return;
		}

		ElapsedDisplay.Text = DurationFormat.FormatDuration(_tracker.GetElapsedTime());
	}

	/// <summary>
	/// Shows an ordinary status message in a quiet guise.
	/// </summary>
	/// <remarks>
	/// The colours come from the theme as a resource reference and no longer from the code.
	/// Hard-coded values were calculated for a light background and became unreadable as soon
	/// as Windows was set to the dark appearance.
	/// </remarks>
	/// <param name="message">The message to show.</param>
	private void ShowStatus(string message)
	{
		StatusHeadline.Text = "Status";
		StatusDisplay.Text = message;
		StatusIcon.Symbol = SymbolRegular.Info24;
		ApplyStatusColors("CardBackground", "CardBorderBrush", "TextFillColorSecondaryBrush");
		ChooseFolderButton.Visibility = Visibility.Collapsed;
	}

	/// <summary>
	/// Shows a message that calls for an action by the user.
	/// </summary>
	/// <remarks>
	/// Colours the message area in the theme's caution colour and brings in the folder button.
	/// In this state the app does nothing of its own accord and waits, as OBERFLAECHE.md,
	/// section 5 demands — but then it must also be visible that it is waiting.
	/// </remarks>
	/// <param name="headline">The bold headline naming the problem.</param>
	/// <param name="message">The detailed message below it.</param>
	/// <param name="offerFolderChoice">Whether the folder button fits the case; not so for a missing module list.</param>
	private void ShowProblem(string headline, string message, bool offerFolderChoice = true)
	{
		LeaveCompactMode();

		StatusHeadline.Text = headline;
		StatusDisplay.Text = message;
		StatusIcon.Symbol = SymbolRegular.Warning24;
		ApplyStatusColors(
			"SystemFillColorCautionBackgroundBrush",
			"SystemFillColorCautionBrush",
			"SystemFillColorCautionBrush");

		// With an empty module list the data folder is fine; picking it again would be the
		// wrong advice. The way there leads through the button for maintaining modules.
		ChooseFolderButton.Visibility = offerFolderChoice ? Visibility.Visible : Visibility.Collapsed;
		UpdateControlState();
	}

	/// <summary>
	/// Sets the three colours of the message box as a reference into the theme.
	/// </summary>
	/// <remarks>
	/// A resource reference is set, not a finished brush. That is the only way the box follows
	/// along when Windows is switched from light to dark while the app runs: a brush assigned
	/// once would stay as it was and would then stand in the wrong colour on the new
	/// background.
	/// </remarks>
	/// <param name="backgroundKey">The resource name for the fill.</param>
	/// <param name="borderKey">The resource name for the border.</param>
	/// <param name="accentKey">The resource name for symbol and explanatory text.</param>
	private void ApplyStatusColors(string backgroundKey, string borderKey, string accentKey)
	{
		StatusPanel.SetResourceReference(BackgroundProperty, backgroundKey);
		StatusPanel.SetResourceReference(Border.BorderBrushProperty, borderKey);
		StatusIcon.SetResourceReference(ForegroundProperty, accentKey);
		StatusDisplay.SetResourceReference(ForegroundProperty, accentKey);
	}

	/// <summary>
	/// Opens the report window, or brings the one already open to the front.
	/// </summary>
	/// <remarks>
	/// The window is deliberately not modal: tracking is meant to carry on while the report is
	/// being looked at. At most one is opened — a second click activates the existing one
	/// instead of putting another beside it.
	/// </remarks>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleReportClick(object sender, RoutedEventArgs eventArguments)
	{
		if (_workspace is null)
		{
			return;
		}

		if (_reportWindow is not null)
		{
			_reportWindow.Activate();

			return;
		}

		_reportWindow = new ReportWindow(_workspace)
		{
			Owner = this,
		};

		_reportWindow.Closed += HandleReportWindowClosed;
		_reportWindow.Show();
	}

	/// <summary>
	/// Forgets the report window as soon as it has been closed.
	/// </summary>
	/// <param name="sender">The closed window.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleReportWindowClosed(object? sender, EventArgs eventArguments)
	{
		_reportWindow = null;
	}

	/// <summary>
	/// Switches into the compact view.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleCompactClick(object sender, RoutedEventArgs eventArguments)
	{
		EnterCompactMode();
	}

	/// <summary>
	/// Switches back from the compact view into the full one.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleExpandClick(object sender, RoutedEventArgs eventArguments)
	{
		LeaveCompactMode();
	}

	/// <summary>
	/// Draws the window together to time display, play and stop and lays it over all others.
	/// </summary>
	/// <remarks>
	/// It stays the same window with the same buttons; only the visibility changes. Play and
	/// stop already carry symbols and therefore need to swap nothing. A second window would
	/// have to share the state of the timer with this one — and that is exactly where the bugs
	/// come from that take a long time to find afterwards.
	/// </remarks>
	/// <remarks>
	/// The measurements are fixed and do not come from the content. Drawing together through
	/// <c>SizeToContent</c> would be more elegant but makes WPF determine the window's scaling
	/// anew: the window came back from the compact view one and a half times larger. Fixed
	/// measurements leave the scaling untouched, and the way there and back is symmetric.
	/// </remarks>
	private void EnterCompactMode()
	{
		if (_isCompactMode)
		{
			return;
		}

		_fullWidth = Width;
		_fullHeight = Height;
		_isCompactMode = true;

		SetFullViewVisibility(Visibility.Collapsed);
		ExpandButton.Visibility = Visibility.Visible;

		// Minimizing falls away here. For one thing it would contradict the intent of this
		// view, which lies over all other windows for that very reason; for another the narrow
		// title bar needs the room so the close cross stays inside the window.
		WindowTitleBar.ShowMinimize = false;

		// For the same reason the two tools disappear from the title bar. They are of no use
		// here anyway: maintaining the module list makes no sense without a visible selection,
		// and the appearance is not switched in passing.
		TitleBarTools.Visibility = Visibility.Collapsed;

		ElapsedDisplay.FontSize = CompactClockFontSize;

		MinWidth = 0;
		MinHeight = 0;
		Width = CompactWindowWidth;
		Height = CompactWindowHeight;
		ResizeMode = ResizeMode.NoResize;
		Topmost = true;

		UpdateWindowTitle();
	}

	/// <summary>
	/// Restores the full view with its earlier measurements.
	/// </summary>
	/// <remarks>
	/// Called by <see cref="ShowProblem"/> as well: messages belong in the message area
	/// (OBERFLAECHE.md, section 5), and that is not visible when compact. Does nothing when the
	/// full view is on anyway.
	/// </remarks>
	private void LeaveCompactMode()
	{
		if (!_isCompactMode)
		{
			return;
		}

		_isCompactMode = false;

		Topmost = false;
		ResizeMode = ResizeMode.CanResize;
		WindowTitleBar.ShowMinimize = true;
		TitleBarTools.Visibility = Visibility.Visible;

		ElapsedDisplay.FontSize = FullClockFontSize;

		ExpandButton.Visibility = Visibility.Collapsed;
		SetFullViewVisibility(Visibility.Visible);

		MinWidth = FullMinWidth;
		MinHeight = FullMinHeight;
		Width = _fullWidth;
		Height = _fullHeight;

		UpdateWindowTitle();
	}

	/// <summary>
	/// Shows or hides the controls that exist only in the full view.
	/// </summary>
	/// <param name="visibility">The visibility to set.</param>
	private void SetFullViewVisibility(Visibility visibility)
	{
		ModuleLabel.Visibility = visibility;
		ModuleSelection.Visibility = visibility;
		ActivityText.Visibility = visibility;
		SecondaryButtons.Visibility = visibility;
		StatusPanel.Visibility = visibility;
	}

	/// <summary>
	/// Puts the selected module into the title bar while the view is compact.
	/// </summary>
	/// <remarks>
	/// The module selection is not visible when compact. So that it stays readable what the
	/// time is running on, the module name stands in the title bar — without a fourth control
	/// being needed for it. Because a narrow window cuts the title bar off and thereby hides
	/// precisely what matters, the time display carries the same name as a tooltip as well.
	/// </remarks>
	/// <remarks>
	/// The title is set twice: once on the window, where Windows reads it for the taskbar and
	/// the window switcher, and once on the self-drawn title bar, which displays it. Both ways
	/// are needed since the title bar no longer comes from Windows.
	/// </remarks>
	private void UpdateWindowTitle()
	{
		if (_isCompactMode && ModuleSelection.SelectedItem is StudyModule module)
		{
			// The title bar holds the module name alone, without the program name before it:
			// the narrow window has room for only the one. The full title stays on the window
			// itself, where Windows reads it for the taskbar and the window switcher.
			Title = $"Zeiterfassung — {module.Name}";
			WindowTitleBar.Title = ShortenForTitleBar(module.Name);
			ElapsedDisplay.ToolTip = module.Name;

			return;
		}

		Title = "Zeiterfassung";
		WindowTitleBar.Title = Title;
		ElapsedDisplay.ToolTip = null;
	}

	/// <summary>
	/// Shortens a module name to the length that fits into the narrow title bar.
	/// </summary>
	/// <remarks>
	/// See <see cref="CompactTitleMaxLength"/>: an unshortened name would push the window
	/// buttons out of the window. In full the name stands as a tooltip on the time display.
	/// </remarks>
	/// <param name="moduleName">The full module name.</param>
	/// <returns>The name, shortened with an ellipsis where needed.</returns>
	private static string ShortenForTitleBar(string moduleName)
	{
		if (moduleName.Length <= CompactTitleMaxLength)
		{
			return moduleName;
		}

		return string.Concat(moduleName.AsSpan(0, CompactTitleMaxLength - 1).TrimEnd(), "…");
	}

	/// <summary>
	/// Sets the availability of the buttons and fields to the current state.
	/// </summary>
	private void UpdateControlState()
	{
		bool isTracking = _tracker?.IsTracking == true;
		bool canStart = _tracker is not null && !isTracking && ModuleSelection.SelectedItem is StudyModule;

		PlayButton.IsEnabled = canStart;
		StopButton.IsEnabled = isTracking;
		ReportButton.IsEnabled = _workspace is not null;
		CompactButton.IsEnabled = ModuleSelection.SelectedItem is StudyModule;
		ModuleSelection.IsEnabled = !isTracking;
		ActivityText.IsEnabled = !isTracking;

		// Maintaining modules is disabled while a session runs, as the module selection is:
		// the module of the running session already stands in the file, and it should not be
		// possible to pull it out from under it.
		ManageModulesButton.IsEnabled = _workspace is not null && !isTracking;

		UpdateToolTips(isTracking);

		if (!isTracking)
		{
			_elapsedTimer.Stop();
		}
	}

	/// <summary>
	/// Opens the dialog for maintaining the module list.
	/// </summary>
	/// <remarks>
	/// Unlike the report window this dialog is <em>modal</em>. It writes <c>module.json</c>,
	/// and while it is open nothing beside it should be turning the same list. The button is
	/// disabled while a session runs anyway.
	/// </remarks>
	/// <remarks>
	/// The selection list is reloaded only when something was actually changed in the dialog.
	/// The user would otherwise lose the module they had picked by merely looking in.
	/// </remarks>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleManageModulesClick(object sender, RoutedEventArgs eventArguments)
	{
		if (_workspace is null)
		{
			return;
		}

		string? previousSelection = (ModuleSelection.SelectedItem as StudyModule)?.Name;

		ModuleDialog dialog = new(_workspace)
		{
			Owner = this,
		};

		dialog.ShowDialog();

		if (!dialog.WasChanged)
		{
			return;
		}

		ReloadModules(previousSelection);
	}

	/// <summary>
	/// Reloads the module list and restores the previous selection.
	/// </summary>
	/// <param name="previousSelection">The module name selected before, or null.</param>
	private void ReloadModules(string? previousSelection)
	{
		if (_workspace is null)
		{
			return;
		}

		try
		{
			StudyModuleList modules = _workspace.ReadModules();

			ModuleSelection.ItemsSource = modules.ActiveModules;

			if (modules.ActiveModules.Count == 0)
			{
				ShowProblem(
					"Kein Modul steht zur Wahl",
					"Über den Knopf neben der Auswahl lässt sich ein Modul anlegen oder ein "
					+ "deaktiviertes wieder aufnehmen.",
					offerFolderChoice: false);

				return;
			}

			if (previousSelection is not null)
			{
				SelectModule(previousSelection);
			}

			if (ModuleSelection.SelectedItem is null)
			{
				ModuleSelection.SelectedIndex = 0;
			}

			ShowStatus("Die Modul-Liste wurde aktualisiert.");
		}
		catch (DataFileException error)
		{
			ShowProblem("Modul-Liste nicht lesbar", error.Message);
		}

		UpdateControlState();
	}

	/// <summary>
	/// Unfolds the menu for choosing the appearance below the button.
	/// </summary>
	/// <remarks>
	/// One button with a menu instead of three buttons: the appearance is switched rarely and
	/// should take up no room beside play and stop.
	/// </remarks>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleThemeClick(object sender, RoutedEventArgs eventArguments)
	{
		if (ThemeButton.ContextMenu is null)
		{
			return;
		}

		ThemeButton.ContextMenu.PlacementTarget = ThemeButton;
		ThemeButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
		ThemeButton.ContextMenu.IsOpen = true;
	}

	/// <summary>
	/// Takes on the appearance chosen in the menu and remembers it.
	/// </summary>
	/// <remarks>
	/// The choice takes effect at once and is additionally written into
	/// <c>einstellungen.json</c> — but only when a data folder is known at all. Without one
	/// there is no settings file it would belong in; the choice then holds for this session and
	/// is back to the old one on the next start. That is bearable, because without a data
	/// folder no time can be recorded anyway.
	/// </remarks>
	/// <param name="sender">The menu item raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleThemeChoice(object sender, RoutedEventArgs eventArguments)
	{
		if (sender is not MenuItem item
			|| !Enum.TryParse(item.Tag as string, out ThemePreference chosen))
		{
			return;
		}

		AppearanceSetup.ApplyTheme(chosen);
		UpdateThemeMenu();
		SaveThemeChoice(chosen);
	}

	/// <summary>
	/// Writes the chosen appearance into the local settings file.
	/// </summary>
	/// <param name="chosen">The appearance to remember.</param>
	private void SaveThemeChoice(ThemePreference chosen)
	{
		if (_workspace is null)
		{
			return;
		}

		try
		{
			SettingsFile.WriteSettings(_settingsFilePath, new AppSettings(_workspace.DataFolder, chosen));
		}
		catch (DataFileException error)
		{
			ShowProblem("Einstellung nicht gespeichert", error.Message);
		}
	}

	/// <summary>
	/// Puts the tick in the menu on the appearance that currently applies.
	/// </summary>
	private void UpdateThemeMenu()
	{
		ThemePreference current = AppearanceSetup.CurrentPreference;

		ThemeSystemItem.IsChecked = current == ThemePreference.System;
		ThemeLightItem.IsChecked = current == ThemePreference.Light;
		ThemeDarkItem.IsChecked = current == ThemePreference.Dark;
	}

	/// <summary>
	/// Puts the tooltip matching the current state onto every button.
	/// </summary>
	/// <remarks>
	/// A disabled button says <em>why</em> it is disabled instead of merely being grey. That is
	/// the real reason for the tooltips: whoever stands before a grey play button sees nowhere
	/// else that all that is missing is the module selection. So that Windows shows the hint in
	/// that state too, <c>ToolTipService.ShowOnDisabled</c> is switched on for the elements
	/// concerned in the XAML.
	/// </remarks>
	/// <param name="isTracking">Whether a session is currently running.</param>
	private void UpdateToolTips(bool isTracking)
	{
		bool hasWorkspace = _workspace is not null;
		bool hasModule = ModuleSelection.SelectedItem is StudyModule;

		PlayButton.ToolTip = !hasWorkspace
			? "Gesperrt: Es ist noch kein Datenordner gewählt."
			: isTracking
				? "Gesperrt: Es läuft bereits eine Sitzung."
				: hasModule
					? "Die Zeit für das gewählte Modul starten."
					: "Gesperrt: Zuerst ein Modul wählen.";

		StopButton.ToolTip = isTracking
			? "Die laufende Sitzung beenden und den Eintrag abschließen."
			: "Gesperrt: Es läuft gerade keine Sitzung.";

		ReportButton.ToolTip = hasWorkspace
			? "Die Auswertung nach Modulen, Wochen, Tagen und Einträgen öffnen."
			: "Gesperrt: Es ist noch kein Datenordner gewählt.";

		CompactButton.ToolTip = hasModule
			? "Das Fenster auf Zeitanzeige, Play und Stop zusammenziehen. Es liegt dann über allen anderen Fenstern."
			: "Gesperrt: Zuerst ein Modul wählen.";

		ModuleSelection.ToolTip = isTracking
			? "Gesperrt: Das Modul gehört zum laufenden Eintrag und steht schon in der Datei."
			: "Das Modul, auf das die Zeit läuft. Gepflegt wird die Liste in module.json.";

		ActivityText.ToolTip = isTracking
			? "Gesperrt: Die Tätigkeit gehört zum laufenden Eintrag und steht schon in der Datei."
			: "Ein Freitext zu dieser Sitzung, etwa das Kapitel oder die Aufgabe.";

		ManageModulesButton.ToolTip = !hasWorkspace
			? "Gesperrt: Es ist noch kein Datenordner gewählt."
			: isTracking
				? "Gesperrt: Solange eine Sitzung läuft, bleibt die Modul-Liste unangetastet."
				: "Module anlegen, umbenennen, löschen oder aus der Auswahl nehmen.";
	}
}
