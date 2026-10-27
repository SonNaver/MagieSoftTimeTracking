# Development — MagieSoft TimeTracking

> **English version of `docs/ENTWICKLUNG.md`, reflecting the German original as of 27 October
> 2026.** The German version is authoritative; where the two differ, it applies. Document names
> below refer to the German originals in `docs/`; four of them also exist in English in this
> folder.

> **The document to start with.** It answers four questions: What is the program for? How is it
> built? How do I build and check it? And where do I find what I am looking for?
>
> It does not repeat the other documents but points to them — two versions of the same matter
> drift apart over time. The guide in section 7 says which document answers which question.
>
> Every command in section 3 was run before it was written down here. The figures next to them
> come from those runs.

## 1. What the program is for

MagieSoft TimeTracking records how much time goes into which module. The user picks a
module, optionally types an activity as free text and starts the clock
with **Play**; **Stop** halts it and closes the entry. That is the whole core — deliberately kept
"really simple".

Beyond that, three things shape every decision in the code:

**The records travel between two computers**, a PC and a notebook. There is no mechanism of its
own for that: the data files lie in a folder that OneDrive synchronises. To the app it is simply
"a file that is already there".

**Crash safety comes before convenience.** If the program crashes between Play and Stop, no
session that has begun may be lost. That is why the entry is already on disk at Play, and why
every file is written through a side file and then renamed.

**The files must be repairable by hand.** They are indented JSON, the module names are spelled out
in them, and the duration is written redundantly so that a human can read it. This assumption
carries a whole series of decisions — among them that the app has **no** screen for editing
recorded times.

What the program explicitly does **not** do, so that nobody looks for it: it does not synchronise
(OneDrive does that), it sends nothing anywhere, it does not change recorded times afterwards, it
does not import CSV files, and it does not rename a module that already has time booked on it.
The reasons are in `ARCHITECTURE.md`, section 7.

## 2. The solution at a glance

Three projects, held together by `Zeiterfassung.slnx` (the new XML format for solutions).

| Project | Target framework | What it contains | Third-party packages |
|---|---|---|---|
| `Zeiterfassung.Core` | `net10.0` | all of the logic, without any UI | **none** |
| `Zeiterfassung.App` | `net10.0-windows10.0.19041.0` | the WPF shell, wired up by code-behind | LiveCharts2, WPF-UI |
| `Zeiterfassung.Core.Tests` | `net10.0` | 169 tests, for the core only | xUnit |

**The dependency runs in one direction:** shell → core and tests → core. The core knows neither
the shell nor the tests. This is not a matter of style but the reason the logic can be checked
without a window.

**Only the shell has third-party packages**, and each was permitted individually: LiveCharts2 for
the charts in the report and WPF-UI for the look including light and dark.
**The core is free of third-party packages and is meant to stay that way.** Every further package
needs a deliberate check first whether the standard library really does not suffice.

```
MagieSoftTimeTracking/
├── Zeiterfassung.slnx
├── README.md · README.de.md          the GitHub start page, English and German
├── LICENSE                           MIT
├── THIRD-PARTY-NOTICES.txt           the licences of the bundled third-party libraries
├── src/
│   ├── Zeiterfassung.Core/
│   │   ├── Model/            TimeEntry, StudyModule, StudyModuleList, ModuleUsage,
│   │   │                     AppSettings, ThemePreference, DurationFormat
│   │   ├── Storage/          TimeEntryFile, ModuleFile, SettingsFile, DataFileSerializer,
│   │   │                     AtomicFileWriter, DataFileException, five Stored… types
│   │   ├── Workflow/         Workspace, TimeTracker
│   │   └── Reporting/        TimeReport, DateRange, ModuleFilter, ModuleTotal, PeriodTotal, CsvExport
│   └── Zeiterfassung.App/
│       ├── App.xaml(.cs)             start-up, single instance, appearance
│       ├── MainWindow.xaml(.cs)      the main window
│       ├── ReportWindow.xaml(.cs)    the report
│       ├── OpenEntryDialog.xaml(.cs) the dialog for the open entry
│       ├── ModuleDialog.xaml(.cs)    module maintenance
│       ├── AppearanceSetup.cs        light/dark
│       ├── SingleInstanceGuard.cs    the lock against a second start
│       ├── ModuleRow / PeriodRow / EntryRow / ModuleEditRow.cs   display rows
│       └── app.manifest
├── tests/
│   └── Zeiterfassung.Core.Tests/     Model/, Storage/, Workflow/, Reporting/
└── docs/                             this documentation
    ├── en/                           four documents additionally in English
    └── screenshots/                  the README images, taken with demo data
```

## 3. Set up, build, check, ship

**Prerequisites.** Windows 10 version 2004 or later, or Windows 11, and the **.NET 10 SDK**.
Visual Studio 2026 is the development environment, but nothing here requires it — every command
runs in any console. `dotnet --version` shows that the SDK is there; 10.x is expected (checked
with 10.0.301).

**Build.** From the root directory, without arguments — the solution is found automatically:

```bash
dotnet build
```

Builds all three projects. The first time, NuGet downloads the packages; after that it takes about
six seconds. **Zero warnings and zero errors are expected** — the code base is warning-free, and it
is meant to stay that way.

**Check.** Also from the root directory:

```bash
dotnet test
```

Runs in under a second, **169 tests, all passed**. Only the core is checked; the WPF interface is
not tested automatically, because the effort there is high and the benefit low. What
that means for your own contribution is in section 5.

**Run.** To try things out while working:

```bash
dotnet run --project src/Zeiterfassung.App
```

On the very first start the app asks for a folder for its data. For development any empty folder
will do — it does not have to be in OneDrive. The chosen path ends up in
`%LOCALAPPDATA%\Zeiterfassung\einstellungen.json`; deleting that file resets the app to the
"first start" state.

**Ship.** The architecture argument is mandatory:

```bash
dotnet publish src/Zeiterfassung.App -c Release -r win-x64 --self-contained false -o publish
```

Result: **32 files, 50.4 MB** in `publish/` (the directory is in `.gitignore`). Without
`-r win-x64` the native drawing library of SkiaSharp would come along for three architectures at
once and the folder would grow considerably. `--self-contained false` means that the .NET runtime
is **not** shipped; the **.NET 10 Desktop Runtime** must be installed on the target computer.

**The whole folder goes to the other computer**, not just the exe: next to it lie the logic core,
the chart libraries and the native drawing library. If one of them is missing, the app does not
start.

## 4. Two lines that must not be tidied up

Both are in `src/Zeiterfassung.App/Zeiterfassung.App.csproj`, both look like legacy, neither is.
There is a comment above each in the project file — they are repeated here because tidying up
happens precisely when one does not read the file attentively.

**`<CETCompat>false</CETCompat>`** switches off the processor's protection against return-oriented
attacks for this program. Without this line the Windows file and folder dialogs crash — and only
on some computers, which makes hunting for the cause particularly unpleasant.

**`<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>`** names the Windows version
explicitly. The shorter `net10.0-windows` silently means Windows 7; NuGet then falls back to the
.NET Framework flavour of SkiaSharp including OpenTK 3. The price of this setting is the minimum
requirement of Windows 10 version 2004.

## 5. Where to find what

By task rather than by file. The last column is not a courtesy: the documentation belongs to the
change, not after it.

| Task | Files to touch | Documents to update |
|---|---|---|
| A new report | `Reporting/TimeReport.cs` (computing), `ReportWindow.xaml(.cs)` (showing) | `ARCHITECTURE.md` 4, `OBERFLAECHE.md` 6, `DIAGRAMME.md` A4/B4 |
| A field in `zeiten.json` | `Storage/StoredTimeEntry.cs`, `Storage/TimeEntryFile.cs` (**raise `SupportedFormatVersion`**), `Model/TimeEntry.cs` | `DATENFORMAT.md` 2, `DIAGRAMME.md` A3 |
| A field in `module.json` or `einstellungen.json` | the respective `Stored…` type and the file class (**raise the version**) | `DATENFORMAT.md` 3 or 4 |
| A new window | new `*.xaml(.cs)` in the shell, inheriting from `FluentWindow`, registered with `AppearanceSetup.RegisterWindow` | `OBERFLAECHE.md`, `DIAGRAMME.md` A5 |
| Changing a message | `MainWindow.ShowStatus` / `ShowProblem`, otherwise the message in the throwing core class | `OBERFLAECHE.md` 5 |
| Disabling or enabling a button | `UpdateControlState` and `UpdateToolTips` in the respective window — **only there** | `OBERFLAECHE.md` |
| Anything about the clock or the duration | `Model/TimeEntry.cs`, `Workflow/TimeTracker.cs` | `DATENFORMAT.md` 2 |
| Anything about writing to disk | `Storage/AtomicFileWriter.cs` — and read `ARCHITECTURE.md`, section 5 very carefully | `ARCHITECTURE.md` 5 |
| Adding a NuGet package | in the shell only, `*.csproj` | `ENTWICKLUNG.md` 2, `ARCHITECTURE.md` 8 |
| Publishing a new version | — | `docs/en/` (state note), `docs/screenshots/`, `README.md` and `README.de.md` |

**Three rules that apply to almost every change:**

**Domain logic belongs in the core, not in a window.** If a change in the shell makes a decision —
computes, checks, interprets —, it belongs one level down. Otherwise it cannot be tested.

**What goes into the core gets tests.** The test project mirrors the folder structure of the core;
the tests for `Reporting/TimeReport.cs` live in `tests/…/Reporting/TimeReportTests.cs`. The naming
pattern is `Method_Situation_Expectation`.

**A changed file format costs a format version.** The JSON reader rejects unknown fields so that
exactly this cannot happen silently. Whoever adds a field and leaves the version alone makes the
file unreadable on the other computer — and because `module.json` **travels**, both computers must
have the new build before anything is maintained there.

## 6. Class to responsibility

One line per class. Members and relationships are in `DIAGRAMME.md`, part A.

### Core — `Model`: the domain terms

| Class | Responsibility |
|---|---|
| `TimeEntry` | A time entry. Without an end it is the open entry; the duration is always computed afresh |
| `StudyModule` | A module: name and whether it is available for selection |
| `StudyModuleList` | The module list; makes sure no name occurs twice |
| `ModuleUsage` | Answers the one question: has time already been booked on this module? |
| `AppSettings` | The settings of one computer: data folder and appearance; immutable |
| `ThemePreference` | The three choices of appearance: System, Light, Dark |
| `DurationFormat` | Formats a time span as `HH:mm:ss` — the only place that does so |

### Core — `Storage`: the translation to disk

| Class | Responsibility |
|---|---|
| `TimeEntryFile` | Reads and writes `zeiten.json` (format version 1) |
| `ModuleFile` | Reads and writes `module.json` (format version 2) |
| `SettingsFile` | Reads and writes `einstellungen.json` in `AppData\Local` (format version 2) |
| `DataFileSerializer` | The shared JSON settings and the format version check |
| `AtomicFileWriter` | Writes through a side file and renames it — the crash protection |
| `DataFileException` | The only exception this area lets out, with a message in full sentences |
| `StoredTimeFile` · `StoredTimeEntry` | The image of `zeiten.json` as it really looks — all fields nullable |
| `StoredModuleFile` · `StoredModule` | The same for `module.json` |
| `StoredSettingsFile` | The same for `einstellungen.json` |

### Core — `Workflow`: operation

| Class | Responsibility |
|---|---|
| `Workspace` | The chosen data folder as an object; the only class that knows the two file names |
| `TimeTracker` | Runs the session: start, stop, find the open entry, resume, complete |

### Core — `Reporting`: the computing

| Class | Responsibility |
|---|---|
| `TimeReport` | The four reports: per module, per week, per day, and the list of entries; plus the average over a sequence of periods |
| `DateRange` | A period; decides by the **start day** whether an entry belongs to it |
| `ModuleFilter` | The selection of modules a report is restricted to; empty means "all" |
| `ModuleTotal` | The result per module: total, count, and whether the module is still in the list |
| `PeriodTotal` | The result per week or day: total, count and the breakdown per module |
| `CsvExport` | Builds the CSV file; separator and decimal mark come from Windows |

### Shell

| Class | Responsibility |
|---|---|
| `App` | Start-up: fend off a second instance, apply the appearance, open the main window |
| `SingleInstanceGuard` | The named lock object per Windows session; otherwise brings the existing window to the front |
| `AppearanceSetup` | Applies light or dark and registers the windows with Windows — or explicitly deregisters them |
| `MainWindow` | The main window: folder choice, module selection, Play and Stop, clock, compact mode, menus |
| `OpenEntryDialog` | Asks about the open entry: complete it or let it keep running |
| `OpenEntryChoice` | The answer of that dialog: undecided, complete, resume |
| `ModuleDialog` | Add, rename, delete, deactivate modules — depending on what is allowed |
| `ReportWindow` | The report: four tables, three charts, period, module filter, CSV export; not modal |
| `ModuleRow` · `PeriodRow` · `EntryRow` | Ready-formatted display rows for the three tables of the report |
| `ModuleEditRow` | The same for the table of module maintenance, including "time booked?" |

### Tests

169 test cases in twelve classes, plus one helper. The folder structure mirrors that of the core.

| Class | What it checks |
|---|---|
| `TimeEntryTests` | Creating, completing, restoring, the duration — including across the clock change |
| `StudyModuleTests` · `StudyModuleListTests` | Names and their uniqueness, including differing upper and lower case |
| `ModuleUsageTests` | When a module may still be edited |
| `TimeEntryFileTests` | Reading and writing `zeiten.json`, format version, damaged entries, no leftovers |
| `ModuleFileTests` · `SettingsFileTests` | The same for the other two files, including unknown fields and values |
| `WorkspaceTests` | First start, remembered folder, vanished folder, missing files |
| `TimeTrackerTests` | Start and stop, the open entry, and what happens when the file is changed by hand meanwhile |
| `TimeReportTests` | The four reports, gaps between periods, midnight, turn of the year, module filter, breakdown per module, average |
| `DateRangeTests` | The bounds of a period, open at the front as well as at the back |
| `CsvExportTests` | Separators, quotation marks, line breaks in the free text, skipped open entries, module filter |
| `StoppedClock` | Not a test but a staged clock: it lets durations be checked without waiting |

## 7. Guide to the other documents

| Question | Document |
|---|---|
| How is the whole thing cut, and why? | `ARCHITECTURE.md` |
| What do the three files look like, field by field? | `DATENFORMAT.md` — **to be read before any access to the data** |
| What does the app show, and how does it behave? | `OBERFLAECHE.md` (German only) |
| How do the classes fit together, how do the flows run? | `DIAGRAMME.md` |
| Is this also available in English? | `docs/en/` — this document, `ARCHITECTURE.md`, `DATENFORMAT.md` and `DIAGRAMME.md`, each with a state note |

**For the first day, in this order:** this document, then `ARCHITECTURE.md` for the big picture,
then `DATENFORMAT.md` — because almost every change sooner or later ends up at the files.
`DIAGRAMME.md` is meant for looking things up, not for reading through.
