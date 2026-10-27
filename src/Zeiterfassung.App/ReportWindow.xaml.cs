using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using Zeiterfassung.Core.Model;
using Zeiterfassung.Core.Reporting;
using Zeiterfassung.Core.Storage;
using Zeiterfassung.Core.Workflow;

// As in the main window: the UI library brings identically named types, so only the one type
// actually needed is named instead of pulling in the whole namespace.
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace Zeiterfassung.App;

/// <summary>
/// The report window: totals per module, week and day, plus the entry list.
/// </summary>
/// <remarks>
/// Like the main window it holds no business logic. The arithmetic happens in
/// <see cref="TimeReport"/>; this window picks the period, passes it on, and presents the
/// results as a table and a chart. It reads afresh from <c>zeiten.json</c> on every build —
/// the file is the truth, not a state in memory.
/// </remarks>
public partial class ReportWindow : FluentWindow
{
	/// <summary>
	/// The number of seconds in an hour, for converting the chart values.
	/// </summary>
	private const double SecondsPerHour = 3600;

	/// <summary>
	/// The factor by which the hour axis of weekly and daily chart reaches above the highest column.
	/// </summary>
	/// <remarks>
	/// The free band above leaves the tooltip room to sit above even the highest column.
	/// </remarks>
	private const double TooltipHeadroom = 1.5;

	/// <summary>
	/// The colours of the module columns in the weekly and daily charts.
	/// </summary>
	/// <remarks>
	/// A fixed set of distinguishable hues (the Okabe-Ito palette, chosen for colour-blind
	/// readers), not taken from the theme: the theme knows an accent, not eight categories.
	/// They are mid-tone enough to stand on light and dark ground alike.
	/// </remarks>
	private static readonly SKColor[] ModulePalette =
	[
		new(0x00, 0x72, 0xB2),
		new(0xE6, 0x9F, 0x00),
		new(0x00, 0x9E, 0x73),
		new(0xD5, 0x5E, 0x00),
		new(0x56, 0xB4, 0xE9),
		new(0xCC, 0x79, 0xA7),
		new(0xF0, 0xE4, 0x42),
		new(0x99, 0x99, 0x99),
	];

	private readonly Workspace _workspace;

	/// <summary>
	/// The modules ticked in the filter; empty means all.
	/// </summary>
	private readonly HashSet<string> _selectedModules = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The extra columns that were put into a table for the modules.
	/// </summary>
	private readonly Dictionary<DataGrid, List<DataGridColumn>> _moduleColumns = [];

	/// <summary>
	/// All modules the filter offers, sorted by name.
	/// </summary>
	private List<string> _filterOptions = [];

	private bool _isUpdatingDates;

	private bool _isUpdatingFilter;

	/// <summary>
	/// Creates the report window and fills it at once.
	/// </summary>
	/// <remarks>
	/// The window's language is deliberately set to that of the machine. WPF otherwise assumes
	/// a US format for date fields regardless of the Windows settings — the calendar would
	/// begin on Sunday and the date would read month/day/year.
	/// </remarks>
	/// <param name="workspace">The data folder that is read from.</param>
	public ReportWindow(Workspace workspace)
	{
		InitializeComponent();

		AppearanceSetup.RegisterWindow(this);

		Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);

		_workspace = workspace;

		// SkiaSharp draws the charts with colours passed in explicitly, not with those of the
		// theme. They therefore have to be rebuilt when Windows is switched from light to dark
		// while the app runs — the axis labels would otherwise stand black on black ground.
		Wpf.Ui.Appearance.ApplicationThemeManager.Changed += HandleThemeChanged;
		Closed += HandleWindowClosed;

		WeekChart.PreviewMouseWheel += HandleChartWheel;
		DayChart.PreviewMouseWheel += HandleChartWheel;

		LoadReport();
	}

	/// <summary>
	/// Hides the tooltip of a chart when its wheel zooms.
	/// </summary>
	/// <remarks>
	/// The chart library zooms the columns from under the pointer but leaves the tooltip of
	/// the old column standing, so it would hang in the air until the pointer leaves.
	/// </remarks>
	/// <param name="sender">The chart raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleChartWheel(object sender, System.Windows.Input.MouseWheelEventArgs eventArguments)
	{
		if (sender is LiveChartsCore.SkiaSharpView.WPF.CartesianChart chart)
		{
			chart.Tooltip?.Hide(chart.CoreChart);
		}
	}

	/// <summary>
	/// Rebuilds the report after Windows was switched to light or dark.
	/// </summary>
	/// <param name="currentTheme">The colour scheme now in force.</param>
	/// <param name="accentColor">The accent colour now in force.</param>
	private void HandleThemeChanged(Wpf.Ui.Appearance.ApplicationTheme currentTheme, Color accentColor)
	{
		LoadReport();
	}

	/// <summary>
	/// Unsubscribes the window from watching the colour scheme when it closes.
	/// </summary>
	/// <remarks>
	/// Without this unsubscription the application-wide event would hold the closed window in
	/// memory, and a later change of the colour scheme would run into a window that no longer
	/// exists.
	/// </remarks>
	/// <param name="sender">The window raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleWindowClosed(object? sender, EventArgs eventArguments)
	{
		Wpf.Ui.Appearance.ApplicationThemeManager.Changed -= HandleThemeChanged;
		Closed -= HandleWindowClosed;
	}

	/// <summary>
	/// Rebuilds the report after a change of the period.
	/// </summary>
	/// <param name="sender">The date field raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleDateChanged(object sender, SelectionChangedEventArgs eventArguments)
	{
		if (_isUpdatingDates)
		{
			return;
		}

		LoadReport();
	}

	/// <summary>
	/// Lifts the narrowing and reports on everything again.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleEverythingClick(object sender, RoutedEventArgs eventArguments)
	{
		_isUpdatingDates = true;
		FromDate.SelectedDate = null;
		ToDate.SelectedDate = null;
		_isUpdatingDates = false;

		LoadReport();
	}

	/// <summary>
	/// Opens the list in which the modules are ticked.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleModuleFilterClick(object sender, RoutedEventArgs eventArguments)
	{
		ModuleFilterPopup.IsOpen = true;
	}

	/// <summary>
	/// Clears all ticks, so that every module counts again.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleAllModulesClick(object sender, RoutedEventArgs eventArguments)
	{
		_isUpdatingFilter = true;
		_selectedModules.Clear();

		foreach (CheckBox box in ModuleFilterList.Children.OfType<CheckBox>())
		{
			box.IsChecked = false;
		}

		_isUpdatingFilter = false;

		LoadReport();
	}

	/// <summary>
	/// Takes over a tick set or removed in the module list and rebuilds the report.
	/// </summary>
	/// <param name="sender">The check box raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleModuleCheckChanged(object sender, RoutedEventArgs eventArguments)
	{
		if (_isUpdatingFilter || sender is not CheckBox { Content: string module } box)
		{
			return;
		}

		if (box.IsChecked == true)
		{
			_selectedModules.Add(module);
		}
		else
		{
			_selectedModules.Remove(module);
		}

		LoadReport();
	}

	/// <summary>
	/// Rebuilds the report after the switch between „per module“ and „sum only“.
	/// </summary>
	/// <param name="sender">The option raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleBreakdownChanged(object sender, RoutedEventArgs eventArguments)
	{
		// The option is checked once while the window is still being built, before anything
		// exists to rebuild.
		if (!IsInitialized)
		{
			return;
		}

		LoadReport();
	}

	/// <summary>
	/// Reads the file again and rebuilds the report.
	/// </summary>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleRefreshClick(object sender, RoutedEventArgs eventArguments)
	{
		LoadReport();
	}

	/// <summary>
	/// Writes the entries of the chosen period as a CSV file.
	/// </summary>
	/// <remarks>
	/// What gets written out is what the window shows: the same period, the same entries. The
	/// file is read again for it, so that the export does not rest on a state from twenty
	/// minutes ago.
	/// </remarks>
	/// <param name="sender">The button raising the event.</param>
	/// <param name="eventArguments">The event data.</param>
	private void HandleExportClick(object sender, RoutedEventArgs eventArguments)
	{
		DateRange? range = BuildRange();

		if (range is null)
		{
			return;
		}

		IReadOnlyList<TimeEntry> entries;

		try
		{
			entries = _workspace.ReadEntries();
		}
		catch (DataFileException error)
		{
			ShowProblem(error.Message);

			return;
		}

		string? targetPath = AskForTargetPath();

		if (targetPath is null)
		{
			return;
		}

		WriteCsvFile(targetPath, entries, range, new ModuleFilter(_selectedModules));
	}

	/// <summary>
	/// Lets the user pick the place and name of the CSV file.
	/// </summary>
	/// <returns>The chosen path, or null when cancelled.</returns>
	private string? AskForTargetPath()
	{
		SaveFileDialog dialog = new()
		{
			Title = "Zeiten als CSV-Datei speichern",
			FileName = $"zeiterfassung-{DateTime.Now:yyyy-MM-dd}.csv",
			DefaultExt = ".csv",
			Filter = "CSV-Datei (*.csv)|*.csv",
			AddExtension = true,
		};

		return dialog.ShowDialog(this) == true ? dialog.FileName : null;
	}

	/// <summary>
	/// Writes the CSV text that was produced and reports the result.
	/// </summary>
	/// <remarks>
	/// What gets written is UTF-8 <em>with</em> a byte order mark. Without those three bytes at
	/// the start, Excel reads the file as Windows-1252 and turns „Tätigkeit“ into
	/// „TÃ¤tigkeit“. This is the one place where the writing deliberately differs from the data
	/// files, which carry no mark.
	/// </remarks>
	/// <param name="targetPath">The chosen path of the file.</param>
	/// <param name="entries">The entries that were read.</param>
	/// <param name="range">The chosen period.</param>
	/// <param name="moduleFilter">The chosen modules.</param>
	private void WriteCsvFile(
		string targetPath,
		IReadOnlyList<TimeEntry> entries,
		DateRange range,
		ModuleFilter moduleFilter)
	{
		string csv = CsvExport.BuildEntryCsv(entries, range, CultureInfo.CurrentCulture, moduleFilter);

		try
		{
			File.WriteAllText(targetPath, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
		catch (Exception error) when (error is IOException or UnauthorizedAccessException)
		{
			ShowProblem($"Die Datei konnte nicht geschrieben werden: {error.Message}");

			return;
		}

		ShowNote(BuildExportNote(targetPath, entries, range, moduleFilter));
	}

	/// <summary>
	/// Puts together the message that appears after a successful export.
	/// </summary>
	/// <param name="targetPath">The path of the file that was written.</param>
	/// <param name="entries">The entries that were read.</param>
	/// <param name="range">The chosen period.</param>
	/// <param name="moduleFilter">The chosen modules.</param>
	/// <returns>The text of the message.</returns>
	private static string BuildExportNote(
		string targetPath,
		IReadOnlyList<TimeEntry> entries,
		DateRange range,
		ModuleFilter moduleFilter)
	{
		int exported = CsvExport.CountExportedEntries(entries, range, moduleFilter);
		int skipped = CsvExport.CountSkippedEntries(entries, range, moduleFilter);

		string note = $"{exported} Einträge geschrieben nach\n{targetPath}";

		if (skipped > 0)
		{
			note += skipped == 1
				? "\n\nEin laufender Eintrag wurde ausgelassen — er hat noch keine Dauer."
				: $"\n\n{skipped} laufende Einträge wurden ausgelassen — sie haben noch keine Dauer.";
		}

		return note;
	}

	/// <summary>
	/// Reads the entries, computes the report and fills tables and charts.
	/// </summary>
	/// <remarks>
	/// Every total is computed exactly once and then handed to both table and chart. The two
	/// thereby demonstrably show the same numbers.
	/// </remarks>
	private void LoadReport()
	{
		DateRange? range = BuildRange();

		if (range is null)
		{
			return;
		}

		IReadOnlyList<TimeEntry> entries;
		StudyModuleList? modules;

		try
		{
			entries = _workspace.ReadEntries();
			modules = ReadModulesIfPossible();
		}
		catch (DataFileException error)
		{
			ShowProblem(error.Message);

			return;
		}

		HideMessage();

		// The filter options come from all times ever booked, not from the chosen period: a
		// module must not vanish from the list just because the period holds no time on it.
		RefreshFilterOptions(
			TimeReport.SummarizeByModule(entries, DateRange.Everything, modules)
				.Select(total => total.Module)
				.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
				.ToList());

		ModuleFilter filter = new(_selectedModules);

		IReadOnlyList<ModuleTotal> moduleTotals = TimeReport.SummarizeByModule(entries, range, modules);
		IReadOnlyList<ModuleTotal> filteredModuleTotals = filter.IsActive
			? TimeReport.SummarizeByModule(entries, range, modules, filter)
			: moduleTotals;
		IReadOnlyList<PeriodTotal> weekTotals = TimeReport.SummarizeByWeek(entries, range, filter);
		IReadOnlyList<PeriodTotal> dayTotals = TimeReport.SummarizeByDay(entries, range, filter);

		List<string> seriesModules = filteredModuleTotals.Select(total => total.Module).ToList();

		ShowModules(moduleTotals);
		ShowWeeks(weekTotals, seriesModules);
		ShowDays(dayTotals, seriesModules);
		ShowEntries(entries, range, filter);
		ShowSummary(filteredModuleTotals, range);
		ShowOpenEntry(entries);
	}

	/// <summary>
	/// Brings the module list of the filter up to date with the modules that exist.
	/// </summary>
	/// <remarks>
	/// The check boxes are rebuilt only when the set of names has changed. A rebuild on every
	/// tick would pull the list from under the mouse while it is still open.
	/// </remarks>
	/// <param name="names">The modules to offer, sorted by name.</param>
	private void RefreshFilterOptions(List<string> names)
	{
		_selectedModules.RemoveWhere(selected => !names.Contains(selected, StringComparer.OrdinalIgnoreCase));

		if (!names.SequenceEqual(_filterOptions, StringComparer.Ordinal))
		{
			_filterOptions = names;
			RebuildFilterList();
		}

		ModuleFilterButton.Content = _selectedModules.Count switch
		{
			0 => "alle",
			1 => _selectedModules.First(),
			_ => $"{_selectedModules.Count} von {_filterOptions.Count}",
		};
	}

	/// <summary>
	/// Fills the list of the filter with one check box per module.
	/// </summary>
	private void RebuildFilterList()
	{
		_isUpdatingFilter = true;
		ModuleFilterList.Children.Clear();

		foreach (string name in _filterOptions)
		{
			CheckBox box = new()
			{
				Content = name,
				IsChecked = _selectedModules.Contains(name),
				Margin = new Thickness(0, 2, 0, 2),
			};

			box.Checked += HandleModuleCheckChanged;
			box.Unchecked += HandleModuleCheckChanged;
			ModuleFilterList.Children.Add(box);
		}

		_isUpdatingFilter = false;
	}

	/// <summary>
	/// Forms the chosen period from the two date fields.
	/// </summary>
	/// <returns>The period, or null when the input contradicts itself.</returns>
	private DateRange? BuildRange()
	{
		DateOnly? from = ToDateOnly(FromDate.SelectedDate);
		DateOnly? to = ToDateOnly(ToDate.SelectedDate);

		try
		{
			return DateRange.CreateRange(from, to);
		}
		catch (ArgumentOutOfRangeException)
		{
			ShowProblem("Der Bis-Tag liegt vor dem Von-Tag. Bitte den Zeitraum berichtigen.");

			return null;
		}
	}

	/// <summary>
	/// Reads the module list, as far as it is readable.
	/// </summary>
	/// <remarks>
	/// An unreadable module list must not prevent the report: the times stand in full in
	/// <c>zeiten.json</c>, and the module name lies there in plain text. Without the list every
	/// module counts as no longer listed — nothing more is lost than that.
	/// </remarks>
	/// <returns>The module list, or null when it could not be read.</returns>
	private StudyModuleList? ReadModulesIfPossible()
	{
		try
		{
			return _workspace.ReadModules();
		}
		catch (DataFileException)
		{
			return null;
		}
	}

	/// <summary>
	/// Shows the module totals as a table and as horizontal bars.
	/// </summary>
	/// <remarks>
	/// The order is reversed for the chart: a horizontal bar chart begins at the bottom, but
	/// the largest module is meant to stand on top — just as in the table.
	/// </remarks>
	/// <param name="totals">The module totals that were computed.</param>
	private void ShowModules(IReadOnlyList<ModuleTotal> totals)
	{
		ModuleGrid.ItemsSource = totals
			.Select(total => new ModuleRow(
				total.Module,
				DurationFormat.FormatDuration(total.Total),
				total.EntryCount,
				total.IsKnownModule ? string.Empty : "steht nicht mehr in module.json"))
			.ToList();

		List<ModuleTotal> forChart = totals.Reverse().ToList();

		ModuleChart.Series =
		[
			new RowSeries<double>
			{
				Name = "Zeit",
				Values = forChart.Select(total => total.Total.TotalHours).ToList(),
				Fill = new SolidColorPaint(ReadThemeColor("AccentFillColorDefaultBrush", SKColors.SteelBlue)),
				XToolTipLabelFormatter = point => FormatHours(point.Coordinate.PrimaryValue),
			},
		];

		ModuleChart.XAxes = [CreateHourAxis()];
		ModuleChart.YAxes = [CreateLabelAxis(forChart.Select(total => total.Module).ToList(), labelsRotation: 0)];
		ApplyChartTheme(ModuleChart);
	}

	/// <summary>
	/// Shows the weekly totals as a table and as columns.
	/// </summary>
	/// <param name="totals">The weekly totals that were computed.</param>
	/// <param name="modules">The modules that get a column of their own, in the order of the legend.</param>
	private void ShowWeeks(IReadOnlyList<PeriodTotal> totals, IReadOnlyList<string> modules)
	{
		IReadOnlyList<string> columnModules = ChooseBreakdownModules(modules);

		ConfigureModuleColumns(WeekGrid, columnModules);
		WeekGrid.ItemsSource = totals
			.Select(total => CreatePeriodRow(FormatWeek(total.PeriodStart), total, columnModules))
			.ToList();

		ShowPeriodChart(WeekChart, totals, columnModules, FormatShortWeek, labelsRotation: 0);
	}

	/// <summary>
	/// Shows the daily totals as a table and as columns.
	/// </summary>
	/// <param name="totals">The daily totals that were computed.</param>
	/// <param name="modules">The modules that get a column of their own, in the order of the legend.</param>
	private void ShowDays(IReadOnlyList<PeriodTotal> totals, IReadOnlyList<string> modules)
	{
		IReadOnlyList<string> columnModules = ChooseBreakdownModules(modules);

		ConfigureModuleColumns(DayGrid, columnModules);
		DayGrid.ItemsSource = totals
			.Select(total => CreatePeriodRow(FormatDay(total.PeriodStart), total, columnModules))
			.ToList();

		ShowPeriodChart(DayChart, totals, columnModules, FormatShortDay, labelsRotation: 45);
	}

	/// <summary>
	/// Decides which modules get a column of their own in weekly and daily report.
	/// </summary>
	/// <param name="modules">The modules with time in the chosen period.</param>
	/// <returns>The modules, or none when only the sum is wanted.</returns>
	private IReadOnlyList<string> ChooseBreakdownModules(IReadOnlyList<string> modules)
	{
		return PerModuleOption.IsChecked == true ? modules : [];
	}

	/// <summary>
	/// Turns a period total into a row of the table.
	/// </summary>
	/// <param name="label">The label of the period.</param>
	/// <param name="total">The total of the period.</param>
	/// <param name="modules">The modules whose time gets a cell.</param>
	/// <returns>The finished row.</returns>
	private static PeriodRow CreatePeriodRow(string label, PeriodTotal total, IReadOnlyList<string> modules)
	{
		string[] moduleTimes = modules
			.Select(module => DurationFormat.FormatDuration(
				total.ModuleTimes.TryGetValue(module, out TimeSpan time) ? time : TimeSpan.Zero))
			.ToArray();

		return new PeriodRow(label, DurationFormat.FormatDuration(total.Total), total.EntryCount, moduleTimes);
	}

	/// <summary>
	/// Gives a table one time column per module, between the period and the sum.
	/// </summary>
	/// <remarks>
	/// The columns added last time are removed first. The columns of the layout — period, sum
	/// and number of entries — stay where the XAML put them.
	/// </remarks>
	/// <param name="grid">The table to set up.</param>
	/// <param name="modules">The modules that get a column.</param>
	private void ConfigureModuleColumns(DataGrid grid, IReadOnlyList<string> modules)
	{
		if (_moduleColumns.TryGetValue(grid, out List<DataGridColumn>? oldColumns))
		{
			foreach (DataGridColumn column in oldColumns)
			{
				grid.Columns.Remove(column);
			}
		}

		List<DataGridColumn> newColumns = [];

		for (int index = 0; index < modules.Count; index++)
		{
			DataGridTextColumn column = new()
			{
				Header = modules[index],
				Binding = new Binding($"ModuleTimes[{index}]"),
				Width = 110,
			};

			grid.Columns.Insert(1 + index, column);
			newColumns.Add(column);
		}

		_moduleColumns[grid] = newColumns;
	}

	/// <summary>
	/// Fills a column chart with the totals of a period.
	/// </summary>
	/// <remarks>
	/// Zooming and panning are switched on here, unlike in the module chart: over a few months
	/// there are more columns than can be read side by side. Per module there is one column
	/// beside the other, then the sum, and across all of it the dashed line of the average.
	/// With a single module the sum column is left out: it would only repeat the
	/// module column at the same height.
	/// </remarks>
	/// <param name="chart">The chart to fill.</param>
	/// <param name="totals">The period totals that were computed.</param>
	/// <param name="modules">The modules that get a column of their own; none for the sum only.</param>
	/// <param name="formatLabel">Labels a period briefly for the axis.</param>
	/// <param name="labelsRotation">The rotation of the axis labels in degrees.</param>
	private void ShowPeriodChart(
		LiveChartsCore.SkiaSharpView.WPF.CartesianChart chart,
		IReadOnlyList<PeriodTotal> totals,
		IReadOnlyList<string> modules,
		Func<DateOnly, string> formatLabel,
		double labelsRotation)
	{
		List<ISeries> series = [];

		foreach (string module in modules)
		{
			series.Add(new ColumnSeries<double>
			{
				Name = module,
				Values = totals
					.Select(total => total.ModuleTimes.TryGetValue(module, out TimeSpan time) ? time.TotalHours : 0.0)
					.ToList(),
				Fill = new SolidColorPaint(PickModuleColor(module)),
				YToolTipLabelFormatter = point => FormatHours(point.Coordinate.PrimaryValue),
			});
		}

		if (modules.Count != 1)
		{
			// Beside module columns the sum takes a neutral tone, so that it cannot be mistaken
			// for a module; alone it takes the accent, as the single column always did.
			SKColor sumColor = modules.Count > 0
				? ReadThemeColor("TextFillColorSecondaryBrush", SKColors.Gray)
				: ReadThemeColor("AccentFillColorDefaultBrush", SKColors.SteelBlue);

			series.Add(new ColumnSeries<double>
			{
				Name = "Summe",
				Values = totals.Select(total => total.Total.TotalHours).ToList(),
				Fill = new SolidColorPaint(sumColor),
				YToolTipLabelFormatter = point => FormatHours(point.Coordinate.PrimaryValue),
			});
		}

		if (totals.Count > 0)
		{
			double averageHours = TimeReport.ComputeAverage(totals).TotalHours;

			series.Add(new LineSeries<double>
			{
				Name = "Durchschnitt",
				Values = totals.Select(_ => averageHours).ToList(),
				Fill = null,
				GeometrySize = 0,
				GeometryFill = null,
				GeometryStroke = null,
				LineSmoothness = 0,
				Stroke = new SolidColorPaint(ReadThemeColor("TextFillColorPrimaryBrush", SKColors.Black))
				{
					StrokeThickness = 2,
					PathEffect = new DashEffect([8f, 5f]),
				},
				YToolTipLabelFormatter = point => FormatHours(point.Coordinate.PrimaryValue),
			});
		}

		Axis hourAxis = CreateHourAxis();

		// The tooltip sits above the column under the pointer. Without room above the highest
		// column the chart library pushes it down onto the very column it describes.
		if (totals.Count > 0 && totals.Max(total => total.Total) > TimeSpan.Zero)
		{
			hourAxis.MaxLimit = totals.Max(total => total.Total).TotalHours * TooltipHeadroom;
		}

		chart.Series = series;
		chart.XAxes = [CreateLabelAxis(totals.Select(total => formatLabel(total.PeriodStart)).ToList(), labelsRotation)];
		chart.YAxes = [hourAxis];
		chart.ZoomMode = ZoomAndPanMode.X;
		chart.LegendPosition = LegendPosition.Bottom;

		// Only the column or line under the pointer gets a tooltip. The default shows every
		// series of the period at once, and with a column per module that box covers the very
		// columns one wants to read.
		chart.FindingStrategy = FindingStrategy.ExactMatch;

		// Above the column instead of beside the pointer, where it hid its neighbours.
		chart.TooltipPosition = TooltipPosition.Top;
		ApplyChartTheme(chart);
	}

	/// <summary>
	/// Picks the colour of a module for the charts.
	/// </summary>
	/// <remarks>
	/// The colour follows the place of the module in the full list of the filter, not in the
	/// narrowed one. A module therefore keeps its colour while others are ticked on and off.
	/// </remarks>
	/// <param name="module">The module name.</param>
	/// <returns>A colour of the palette.</returns>
	private SKColor PickModuleColor(string module)
	{
		int index = _filterOptions.FindIndex(name => string.Equals(name, module, StringComparison.OrdinalIgnoreCase));

		return ModulePalette[Math.Max(index, 0) % ModulePalette.Length];
	}

	/// <summary>
	/// Creates the axis on which time is plotted in hours.
	/// </summary>
	/// <remarks>
	/// The axis counts in hours as a decimal number, because a label in the pattern 01:35:00
	/// would make it unreadable. The familiar form stands in the tooltip instead.
	/// </remarks>
	/// <returns>The finished axis.</returns>
	private Axis CreateHourAxis()
	{
		return new Axis
		{
			Name = "Stunden",
			MinLimit = 0,
			NamePaint = new SolidColorPaint(ReadThemeColor("TextFillColorSecondaryBrush", SKColors.Gray)),
			LabelsPaint = new SolidColorPaint(ReadThemeColor("TextFillColorSecondaryBrush", SKColors.Gray)),
			SeparatorsPaint = new SolidColorPaint(ReadThemeColor("ControlStrokeColorDefaultBrush", SKColors.LightGray))
			{
				StrokeThickness = 1,
			},
		};
	}

	/// <summary>
	/// Creates the axis along which the labels of the periods stand.
	/// </summary>
	/// <param name="labels">The labels in the order of the values.</param>
	/// <param name="labelsRotation">The rotation of the labels in degrees.</param>
	/// <returns>The finished axis.</returns>
	private Axis CreateLabelAxis(IReadOnlyList<string> labels, double labelsRotation)
	{
		return new Axis
		{
			Labels = labels.ToList(),
			LabelsRotation = labelsRotation,
			LabelsPaint = new SolidColorPaint(ReadThemeColor("TextFillColorSecondaryBrush", SKColors.Gray)),
			SeparatorsPaint = null,
		};
	}

	/// <summary>
	/// Sets tooltip and legend of a chart to the colours of the theme.
	/// </summary>
	/// <remarks>
	/// Without these settings the chart library draws its tooltip in fixed light colours. On a
	/// dark ground there would then be white text on a white box.
	/// </remarks>
	/// <param name="chart">The chart to set up.</param>
	private void ApplyChartTheme(LiveChartsCore.SkiaSharpView.WPF.CartesianChart chart)
	{
		chart.TooltipTextPaint = new SolidColorPaint(ReadThemeColor("TextFillColorPrimaryBrush", SKColors.Black));
		// A solid brush, not CardBackground: that one is translucent, and columns and the
		// average line would show through the tooltip.
		chart.TooltipBackgroundPaint = new SolidColorPaint(ReadThemeColor("SolidBackgroundFillColorTertiaryBrush", SKColors.White));
		chart.LegendTextPaint = new SolidColorPaint(ReadThemeColor("TextFillColorPrimaryBrush", SKColors.Black));
		chart.LegendBackgroundPaint = new SolidColorPaint(ReadThemeColor("CardBackground", SKColors.White));
	}

	/// <summary>
	/// Reads a colour of the current theme for use in a chart.
	/// </summary>
	/// <remarks>
	/// The chart library draws with SkiaSharp and does not know the resources of WPF; the
	/// colour therefore has to be fetched and converted one by one. If the resource name is
	/// missing or holds no solid brush, the fallback passed in applies — a missing key should
	/// not make the chart disappear.
	/// </remarks>
	/// <param name="resourceKey">The name of the theme resource.</param>
	/// <param name="fallback">The colour that applies when the resource is missing.</param>
	/// <returns>The colour in the form the chart library expects.</returns>
	private SKColor ReadThemeColor(string resourceKey, SKColor fallback)
	{
		if (TryFindResource(resourceKey) is not SolidColorBrush brush)
		{
			return fallback;
		}

		return new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A);
	}

	/// <summary>
	/// Fills the list of the individual entries.
	/// </summary>
	/// <param name="entries">The entries that were read.</param>
	/// <param name="range">The chosen period.</param>
	/// <param name="filter">The chosen modules.</param>
	private void ShowEntries(IReadOnlyList<TimeEntry> entries, DateRange range, ModuleFilter filter)
	{
		EntryGrid.ItemsSource = TimeReport.SelectRecentEntries(entries, range, filter)
			.Select(CreateEntryRow)
			.ToList();
	}

	/// <summary>
	/// Shows the grand total of the period.
	/// </summary>
	/// <param name="totals">The module totals that were computed.</param>
	/// <param name="range">The chosen period.</param>
	private void ShowSummary(IReadOnlyList<ModuleTotal> totals, DateRange range)
	{
		TimeSpan total = TimeSpan.Zero;
		int entryCount = 0;

		foreach (ModuleTotal moduleTotal in totals)
		{
			total += moduleTotal.Total;
			entryCount += moduleTotal.EntryCount;
		}

		string moduleNote = _selectedModules.Count > 0
			? $" · {string.Join(", ", _filterOptions.Where(_selectedModules.Contains))}"
			: string.Empty;

		SummaryDisplay.Text =
			$"Gesamt {DurationFormat.FormatDuration(total)} in {entryCount} Einträgen · {FormatRange(range)}{moduleNote}";
	}

	/// <summary>
	/// Points out a running session that is in no total.
	/// </summary>
	/// <param name="entries">The entries that were read.</param>
	private void ShowOpenEntry(IReadOnlyList<TimeEntry> entries)
	{
		TimeEntry? openEntry = entries.FirstOrDefault(entry => entry.IsOpen);

		if (openEntry is null)
		{
			OpenEntryDisplay.Visibility = Visibility.Collapsed;

			return;
		}

		OpenEntryDisplay.Text =
			$"Eine Sitzung für „{openEntry.Module}“ läuft seit "
			+ $"{openEntry.Start.ToString("g", CultureInfo.CurrentCulture)} und ist in keiner Summe enthalten.";
		OpenEntryDisplay.Visibility = Visibility.Visible;
	}

	/// <summary>
	/// Turns an entry into a row of the entry list.
	/// </summary>
	/// <param name="entry">The entry.</param>
	/// <returns>The finished row.</returns>
	private static EntryRow CreateEntryRow(TimeEntry entry)
	{
		string period = entry.End is null
			? $"{entry.Start:HH:mm} – offen"
			: $"{entry.Start:HH:mm} – {entry.End.Value:HH:mm}";

		string duration = entry.Duration is null
			? "läuft"
			: DurationFormat.FormatDuration(entry.Duration.Value);

		return new EntryRow(
			entry.Start.ToString("ddd, dd.MM.yyyy", CultureInfo.CurrentCulture),
			period,
			entry.Module,
			entry.Note,
			duration);
	}

	/// <summary>
	/// Turns a chart value in hours back into the familiar time form.
	/// </summary>
	/// <remarks>
	/// Rounded to whole seconds. The chart values are floating-point numbers; without the
	/// rounding the tooltip would occasionally be a second short.
	/// </remarks>
	/// <param name="hours">The value of the axis in hours.</param>
	/// <returns>The time in the pattern hours:minutes:seconds.</returns>
	private static string FormatHours(double hours)
	{
		return DurationFormat.FormatDuration(TimeSpan.FromSeconds(Math.Round(hours * SecondsPerHour)));
	}

	/// <summary>
	/// Labels a calendar week with its number and its outer days.
	/// </summary>
	/// <param name="weekStart">The Monday of the week.</param>
	/// <returns>The label of the week.</returns>
	private static string FormatWeek(DateOnly weekStart)
	{
		DateTime asDateTime = weekStart.ToDateTime(TimeOnly.MinValue);

		return $"KW {ISOWeek.GetWeekOfYear(asDateTime)}/{ISOWeek.GetYear(asDateTime)} "
			+ $"({weekStart:dd.MM.} – {weekStart.AddDays(6):dd.MM.yyyy})";
	}

	/// <summary>
	/// Labels a calendar week briefly for the axis of a chart.
	/// </summary>
	/// <param name="weekStart">The Monday of the week.</param>
	/// <returns>The short label of the week.</returns>
	private static string FormatShortWeek(DateOnly weekStart)
	{
		return $"KW {ISOWeek.GetWeekOfYear(weekStart.ToDateTime(TimeOnly.MinValue))}";
	}

	/// <summary>
	/// Labels a day with weekday and date.
	/// </summary>
	/// <param name="day">The day.</param>
	/// <returns>The label of the day.</returns>
	private static string FormatDay(DateOnly day)
	{
		return day.ToString("ddd, dd.MM.yyyy", CultureInfo.CurrentCulture);
	}

	/// <summary>
	/// Labels a day briefly for the axis of a chart.
	/// </summary>
	/// <param name="day">The day.</param>
	/// <returns>The short label of the day.</returns>
	private static string FormatShortDay(DateOnly day)
	{
		return day.ToString("dd.MM.", CultureInfo.CurrentCulture);
	}

	/// <summary>
	/// Describes the chosen period in words.
	/// </summary>
	/// <param name="range">The chosen period.</param>
	/// <returns>The description of the period.</returns>
	private static string FormatRange(DateRange range)
	{
		if (range.IsUnbounded)
		{
			return "gesamter Zeitraum";
		}

		if (range.From is null)
		{
			return $"bis {range.To!.Value:dd.MM.yyyy}";
		}

		if (range.To is null)
		{
			return $"ab {range.From.Value:dd.MM.yyyy}";
		}

		return $"{range.From.Value:dd.MM.yyyy} bis {range.To.Value:dd.MM.yyyy}";
	}

	/// <summary>
	/// Turns the date of a date field into a day without a time.
	/// </summary>
	/// <param name="value">The value of the date field.</param>
	/// <returns>The day, or null when the field is empty.</returns>
	private static DateOnly? ToDateOnly(DateTime? value)
	{
		return value is null ? null : DateOnly.FromDateTime(value.Value);
	}

	/// <summary>
	/// Shows an error below the tables.
	/// </summary>
	/// <param name="message">The text of the message.</param>
	private void ShowProblem(string message)
	{
		ShowMessage(message, "SystemFillColorCriticalBrush");
	}

	/// <summary>
	/// Shows a quiet note below the tables.
	/// </summary>
	/// <param name="message">The text of the message.</param>
	private void ShowNote(string message)
	{
		ShowMessage(message, "TextFillColorSecondaryBrush");
	}

	/// <summary>
	/// Shows a message below the tables.
	/// </summary>
	/// <remarks>
	/// A reference into the theme is set and not a finished brush, so that the message follows
	/// along when Windows is switched from light to dark while the app runs.
	/// </remarks>
	/// <param name="message">The text of the message.</param>
	/// <param name="colorResourceKey">The resource name of the colour carrying the tone.</param>
	private void ShowMessage(string message, string colorResourceKey)
	{
		MessageDisplay.Text = message;
		MessageDisplay.SetResourceReference(ForegroundProperty, colorResourceKey);
		MessageDisplay.Visibility = Visibility.Visible;
	}

	/// <summary>
	/// Hides the message again.
	/// </summary>
	private void HideMessage()
	{
		MessageDisplay.Visibility = Visibility.Collapsed;
	}
}
