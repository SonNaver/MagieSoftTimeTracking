using System.Windows;
using System.Windows.Controls;
using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Storage;
using Zeiterfassung.Core.Workflow;

// As in the other windows: only the types actually needed from the UI library, because a
// blanket "using" made the identically named WPF types ambiguous.
using FluentMessageBox = Wpf.Ui.Controls.MessageBox;
using FluentMessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Zeiterfassung.App;

/// <summary>
/// The dialog in which the module list is maintained.
/// </summary>
/// <remarks>
/// Like the other windows it holds no business logic. Whether a module can still be
/// changed freely is decided by <see cref="ModuleUsage"/> in the core; this window displays
/// the result and switches its buttons accordingly.
/// </remarks>
/// <remarks>
/// Every change is written at once, not only on closing. That fits the crash safety which is a
/// core concern of this app, and it spares a state of „changed but not yet saved“ that the
/// user would have to keep in mind.
/// </remarks>
public partial class ModuleDialog : FluentWindow
{
	private readonly Workspace _workspace;

	private IReadOnlySet<string> _usedModuleNames = new HashSet<string>();
	private List<StudyModule> _modules = [];
	private bool _isListReadable;

	/// <summary>
	/// Creates the module dialog and fills it from the files of the data folder.
	/// </summary>
	/// <param name="workspace">The data folder that is read from and written to.</param>
	public ModuleDialog(Workspace workspace)
	{
		InitializeComponent();

		AppearanceSetup.RegisterWindow(this);

		_workspace = workspace;

		LoadModules();
	}

	/// <summary>
	/// States whether the module list was changed in this dialog.
	/// </summary>
	/// <remarks>
	/// The main window reloads its selection list only when something has changed — otherwise
	/// merely looking in would lose the module the user had picked.
	/// </remarks>
	public bool WasChanged { get; private set; }

	/// <summary>
	/// Reads modules and entries afresh and builds the list anew.
	/// </summary>
	/// <remarks>
	/// Every build reads from the files and not from a state in memory. The files travel
	/// through OneDrive and may have been repaired by hand.
	/// </remarks>
	/// <remarks>
	/// If the reading fails, the dialog stays <em>read-only</em>. That is not convenience but
	/// protection against data loss: the internal list would then be empty, and adding a module
	/// would write it back as a whole — the existing modules of the unreadable file would
	/// thereby be gone. The user repairs the file by hand and opens the dialog again.
	/// </remarks>
	private void LoadModules()
	{
		_isListReadable = false;

		try
		{
			_modules = [.. _workspace.ReadModules().Modules];
			_usedModuleNames = ModuleUsage.CollectUsedModuleNames(_workspace.ReadEntries());
			_isListReadable = true;
		}
		catch (DataFileException error)
		{
			_modules = [];
			ModuleGrid.ItemsSource = null;
			ShowMessage(
				error.Message + "\n\nSolange die Datei nicht gelesen werden kann, lässt sich hier "
				+ "nichts ändern — sonst gingen die darin stehenden Module verloren.",
				SymbolRegular.Warning24,
				"SystemFillColorCautionBrush");
			UpdateButtonState();

			return;
		}

		ShowModules();
	}

	/// <summary>
	/// Fills the list from the current state of the modules.
	/// </summary>
	/// <param name="nameToSelect">The module name that is to be selected afterwards, or null.</param>
	private void ShowModules(string? nameToSelect = null)
	{
		ModuleGrid.ItemsSource = _modules
			.Select(module => new ModuleEditRow(
				module.Name,
				module.IsActive,
				!ModuleUsage.CanBeEdited(module.Name, _usedModuleNames)))
			.ToList();

		if (nameToSelect is not null)
		{
			ModuleGrid.SelectedItem = ModuleGrid.ItemsSource
				.OfType<ModuleEditRow>()
				.FirstOrDefault(row => row.Name == nameToSelect);
		}

		UpdateButtonState();
	}

	/// <summary>
	/// Takes the name of the selected module into the input field.
	/// </summary>
	/// <param name="sender">The list raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleSelectionChanged(object sender, SelectionChangedEventArgs eventArguments)
	{
		if (ModuleGrid.SelectedItem is ModuleEditRow row)
		{
			NameInput.Text = row.Name;
		}

		UpdateButtonState();
	}

	/// <summary>
	/// Sets the buttons to match the current input.
	/// </summary>
	/// <param name="sender">The text box raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleNameChanged(object sender, TextChangedEventArgs eventArguments)
	{
		UpdateButtonState();
	}

	/// <summary>
	/// Creates a new module under the name that was typed in.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleAddClick(object sender, RoutedEventArgs eventArguments)
	{
		string name = NameInput.Text.Trim();

		if (FindExistingModule(name) is not null)
		{
			ShowMessage(
				$"Ein Modul namens „{name}“ gibt es bereits.",
				SymbolRegular.Warning24,
				"SystemFillColorCautionBrush");

			return;
		}

		_modules.Add(new StudyModule(name));

		if (SaveModules())
		{
			NameInput.Text = string.Empty;
			ShowModules(name);
			ShowMessage($"„{name}“ wurde angelegt.", SymbolRegular.CheckmarkCircle24, "TextFillColorSecondaryBrush");
		}
	}

	/// <summary>
	/// Renames the selected module to the name that was typed in.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleRenameClick(object sender, RoutedEventArgs eventArguments)
	{
		if (ModuleGrid.SelectedItem is not ModuleEditRow row)
		{
			return;
		}

		string newName = NameInput.Text.Trim();
		StudyModule? clash = FindExistingModule(newName);

		if (clash is not null && clash.Name != row.Name)
		{
			ShowMessage(
				$"Ein Modul namens „{newName}“ gibt es bereits.",
				SymbolRegular.Warning24,
				"SystemFillColorCautionBrush");

			return;
		}

		int position = _modules.FindIndex(module => module.Name == row.Name);

		if (position < 0)
		{
			return;
		}

		_modules[position] = new StudyModule(newName, _modules[position].IsActive);

		if (SaveModules())
		{
			ShowModules(newName);
			ShowMessage(
				$"„{row.Name}“ heißt jetzt „{newName}“.",
				SymbolRegular.CheckmarkCircle24,
				"TextFillColorSecondaryBrush");
		}
	}

	/// <summary>
	/// Deletes the selected module after a confirmation.
	/// </summary>
	/// <remarks>
	/// The confirmation is one of the few exceptions to the rule that messages belong in the
	/// message area (OBERFLAECHE.md, section 5): here an answer is needed before anything can
	/// go on. Only a module without a single entry can be deleted anyway — so no recorded time
	/// is lost, only a name.
	/// </remarks>
	/// <remarks>
	/// The question is put with the message box of the UI library and not with the one from
	/// Windows. The latter comes up light and in the styling of 2009 and would tear a hole in
	/// the appearance in the middle of the interaction.
	/// </remarks>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private async void HandleDeleteClick(object sender, RoutedEventArgs eventArguments)
	{
		if (ModuleGrid.SelectedItem is not ModuleEditRow row)
		{
			return;
		}

		FluentMessageBox question = new()
		{
			Title = "Modul löschen",
			Content = $"Das Modul „{row.Name}“ wirklich löschen?\n\n"
				+ "Es geht dabei keine erfasste Zeit verloren — auf dieses Modul ist noch keine gebucht.",
			PrimaryButtonText = "Löschen",
			CloseButtonText = "Abbrechen",
			Owner = this,
		};

		if (await question.ShowDialogAsync() != FluentMessageBoxResult.Primary)
		{
			return;
		}

		_modules.RemoveAll(module => module.Name == row.Name);

		if (SaveModules())
		{
			NameInput.Text = string.Empty;
			ShowModules();
			ShowMessage($"„{row.Name}“ wurde gelöscht.", SymbolRegular.CheckmarkCircle24, "TextFillColorSecondaryBrush");
		}
	}

	/// <summary>
	/// Switches the selected module between active and deactivated.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleToggleClick(object sender, RoutedEventArgs eventArguments)
	{
		if (ModuleGrid.SelectedItem is not ModuleEditRow row)
		{
			return;
		}

		int position = _modules.FindIndex(module => module.Name == row.Name);

		if (position < 0)
		{
			return;
		}

		bool nowActive = !_modules[position].IsActive;

		_modules[position] = new StudyModule(row.Name, nowActive);

		if (SaveModules())
		{
			ShowModules(row.Name);
			ShowMessage(
				nowActive
					? $"„{row.Name}“ steht wieder zur Wahl."
					: $"„{row.Name}“ ist deaktiviert und verschwindet aus der Auswahl.",
				SymbolRegular.CheckmarkCircle24,
				"TextFillColorSecondaryBrush");
		}
	}

	/// <summary>
	/// Writes the module list into the data folder.
	/// </summary>
	/// <remarks>
	/// If the write fails, the state read earlier is restored. The list would otherwise show a
	/// change that never reached the disk.
	/// </remarks>
	/// <returns>True when the write succeeded.</returns>
	private bool SaveModules()
	{
		try
		{
			_workspace.WriteModules(new StudyModuleList(_modules));
			WasChanged = true;

			return true;
		}
		catch (Exception error) when (error is DataFileException or ArgumentException)
		{
			ShowMessage(error.Message, SymbolRegular.Warning24, "SystemFillColorCautionBrush");
			LoadModules();

			return false;
		}
	}

	/// <summary>
	/// Looks for a module with the given name in the current list.
	/// </summary>
	/// <remarks>
	/// The comparison ignores case, as in the core: two names that differ only in case are the
	/// same module to the user.
	/// </remarks>
	/// <param name="name">The module name to look for.</param>
	/// <returns>The module that was found, or null.</returns>
	private StudyModule? FindExistingModule(string name)
	{
		return _modules.FirstOrDefault(module =>
			string.Equals(module.Name, name, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Sets the availability of the buttons to match selection and input.
	/// </summary>
	private void UpdateButtonState()
	{
		string name = NameInput.Text.Trim();
		bool hasName = name.Length > 0;
		ModuleEditRow? row = ModuleGrid.SelectedItem as ModuleEditRow;

		AddButton.IsEnabled = _isListReadable && hasName && FindExistingModule(name) is null;
		RenameButton.IsEnabled = _isListReadable && row is not null && !row.IsUsed && hasName && name != row.Name;
		DeleteButton.IsEnabled = _isListReadable && row is not null && !row.IsUsed;
		ToggleButton.IsEnabled = _isListReadable && row is not null;

		ToggleButton.Content = row?.IsActive == false ? "Aktivieren" : "Deaktivieren";
		ToggleButton.Icon = new SymbolIcon
		{
			Symbol = row?.IsActive == false ? SymbolRegular.Eye24 : SymbolRegular.EyeOff24,
		};

		UpdateToolTips(row, hasName, name);
	}

	/// <summary>
	/// Puts the tooltip matching the current state onto every button.
	/// </summary>
	/// <param name="row">The selected row, or null.</param>
	/// <param name="hasName">Whether a name stands in the input field.</param>
	/// <param name="name">The name that was typed in.</param>
	private void UpdateToolTips(ModuleEditRow? row, bool hasName, string name)
	{
		if (!_isListReadable)
		{
			const string blocked = "Gesperrt: Die Modul-Liste konnte nicht gelesen werden.";

			AddButton.ToolTip = blocked;
			RenameButton.ToolTip = blocked;
			DeleteButton.ToolTip = blocked;
			ToggleButton.ToolTip = blocked;

			return;
		}

		AddButton.ToolTip = !hasName
			? "Gesperrt: Zuerst einen Namen eingeben."
			: FindExistingModule(name) is not null
				? $"Gesperrt: „{name}“ gibt es bereits."
				: "Das Modul unter diesem Namen neu anlegen.";

		RenameButton.ToolTip = row is null
			? "Gesperrt: Zuerst ein Modul in der Liste wählen."
			: row.IsUsed
				? "Gesperrt: Auf dieses Modul ist bereits Zeit gebucht. Sein Name steht in jedem dieser Einträge."
				: !hasName
					? "Gesperrt: Zuerst einen neuen Namen eingeben."
					: name == row.Name
						? "Gesperrt: Der Name ist unverändert."
						: "Das gewählte Modul auf den eingegebenen Namen umbenennen.";

		DeleteButton.ToolTip = row is null
			? "Gesperrt: Zuerst ein Modul in der Liste wählen."
			: row.IsUsed
				? "Gesperrt: Auf dieses Modul ist bereits Zeit gebucht. Es kann nur noch deaktiviert werden."
				: "Das gewählte Modul löschen. Es geht keine erfasste Zeit verloren.";

		ToggleButton.ToolTip = row is null
			? "Gesperrt: Zuerst ein Modul in der Liste wählen."
			: row.IsActive
				? "Das Modul aus der Auswahl nehmen. Seine Zeit zählt in der Auswertung weiter mit."
				: "Das Modul wieder in die Auswahl aufnehmen.";
	}

	/// <summary>
	/// Shows a message in the box at the bottom edge of the dialog.
	/// </summary>
	/// <param name="message">The text of the message.</param>
	/// <param name="symbol">The symbol carrying the tone of the message.</param>
	/// <param name="colorResourceKey">The resource name of the colour for symbol and border.</param>
	private void ShowMessage(string message, SymbolRegular symbol, string colorResourceKey)
	{
		MessageDisplay.Text = message;
		MessageIcon.Symbol = symbol;
		MessageIcon.SetResourceReference(ForegroundProperty, colorResourceKey);
		MessagePanel.Visibility = Visibility.Visible;
	}
}
