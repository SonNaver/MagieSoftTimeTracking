using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Zeiterfassung.Core.Model;

// As in the other windows: only the type actually needed from the UI library, because a
// blanket "using" made the identically named WPF types ambiguous.
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace Zeiterfassung.App;

/// <summary>
/// The user's choice on starting up with an open entry.
/// </summary>
public enum OpenEntryChoice
{
	/// <summary>
	/// The user has not decided; the entry stays open.
	/// </summary>
	Undecided,

	/// <summary>
	/// The entry is closed with an end that was typed in.
	/// </summary>
	Complete,

	/// <summary>
	/// Time carries on from the stored start.
	/// </summary>
	Resume,
}

/// <summary>
/// The dialog that appears on starting up with an open entry.
/// </summary>
/// <remarks>
/// Offers both ways — complete or keep running — <em>every</em> time. The app deliberately does not
/// distinguish whether the open entry is from an hour ago or from yesterday — which is why the
/// choice lies with the user, the only one who can know.
/// </remarks>
public partial class OpenEntryDialog : FluentWindow
{
	/// <summary>
	/// The pattern in which the end is typed in and proposed.
	/// </summary>
	private const string EndInputPattern = "dd.MM.yyyy HH:mm:ss";

	private readonly TimeEntry _openEntry;

	/// <summary>
	/// Creates the dialog for an open entry.
	/// </summary>
	/// <param name="openEntry">The open entry from the time file.</param>
	public OpenEntryDialog(TimeEntry openEntry)
	{
		InitializeComponent();

		_openEntry = openEntry;
		Choice = OpenEntryChoice.Undecided;

		EntryDescription.Text =
			$"Modul: {openEntry.Module}\n"
			+ $"Begonnen: {openEntry.Start.ToString(EndInputPattern, CultureInfo.CurrentCulture)}\n"
			+ (string.IsNullOrWhiteSpace(openEntry.Note) ? string.Empty : $"Tätigkeit: {openEntry.Note}\n")
			+ $"Seither vergangen: {DurationFormat.FormatDuration(DateTimeOffset.Now - openEntry.Start)}";

		EndInput.Text = DateTimeOffset.Now.ToString(EndInputPattern, CultureInfo.CurrentCulture);
	}

	/// <summary>
	/// What the user has decided on.
	/// </summary>
	public OpenEntryChoice Choice { get; private set; }

	/// <summary>
	/// The end that was typed in, if the entry is to be closed.
	/// </summary>
	public DateTimeOffset ChosenEnd { get; private set; }

	/// <summary>
	/// Closes the entry with the end that was typed in.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleCompleteClick(object sender, RoutedEventArgs eventArguments)
	{
		if (!TryReadEnd(out DateTimeOffset end))
		{
			return;
		}

		Choice = OpenEntryChoice.Complete;
		ChosenEnd = end;
		Close();
	}

	/// <summary>
	/// Lets time carry on from the stored start.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleResumeClick(object sender, RoutedEventArgs eventArguments)
	{
		Choice = OpenEntryChoice.Resume;
		Close();
	}

	/// <summary>
	/// Checks the input on every change and disables completing while it will not do.
	/// </summary>
	/// <param name="sender">The text box raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleEndInputChanged(object sender, TextChangedEventArgs eventArguments)
	{
		bool isEndValid = TryReadEnd(out _);

		CompleteButton.IsEnabled = isEndValid;
		CompleteButton.ToolTip = isEndValid
			? "Den offenen Eintrag mit diesem Ende abschließen."
			: "Gesperrt: Die Eingabe ist noch kein Zeitpunkt nach dem Beginn.";
	}

	/// <summary>
	/// Reads the end that was typed in and checks it against the start.
	/// </summary>
	/// <remarks>
	/// Typed in without a time zone, the local time of this machine applies — the same rule as
	/// for a hand repair of the file. The check here does not replace the core's, it
	/// merely spares the user an error message after the click.
	/// </remarks>
	/// <param name="end">The end that was read, when the input will do.</param>
	/// <returns>True when the input is a point in time after the start.</returns>
	private bool TryReadEnd(out DateTimeOffset end)
	{
		end = default;

		if (!DateTime.TryParse(
			EndInput.Text,
			CultureInfo.CurrentCulture,
			DateTimeStyles.None,
			out DateTime parsed))
		{
			ShowHint($"Bitte Datum und Uhrzeit eingeben, zum Beispiel {EndInputPattern}.");

			return false;
		}

		end = new DateTimeOffset(parsed, TimeZoneInfo.Local.GetUtcOffset(parsed));

		if (end < _openEntry.Start)
		{
			ShowHint("Das Ende liegt vor dem Beginn der Sitzung.");

			return false;
		}

		ShowHint(string.Empty);

		return true;
	}

	/// <summary>
	/// Shows a hint about the input or hides it.
	/// </summary>
	/// <param name="hint">The hint text; empty hides it.</param>
	private void ShowHint(string hint)
	{
		InputHint.Text = hint;
	}
}
