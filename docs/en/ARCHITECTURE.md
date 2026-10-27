# Architecture — MagieSoft TimeTracking

> **English version of `docs/ARCHITECTURE.md`, reflecting the German original as of 27 October
> 2026.** The German version is authoritative; where the two differ, it applies. Document names
> below refer to the German originals in `docs/`; four of them also exist in English in this
> folder.

> This document describes the current state, not how it came about.
>
> **The same in diagrams is in `DIAGRAMME.md`**: all 38 classes with their members and
> relationships, and six flows, each in a diagram of its own. This document remains the shorter
> version in prose; whoever wants the details looks them up there.
>
> **Newcomers start with `ENTWICKLUNG.md`.** It explains how to build, check and ship the
> project, which class is responsible for what, and where to begin for a particular task.

## 1. Context of use

MagieSoft TimeTracking records how much time the user spends per module. The user picks a module,
optionally types an activity as free text and starts the clock with Play; Stop halts it and closes
the entry. The user is a single person who works on two computers — a PC and a notebook.

The app runs as an ordinary Windows program. It has no counterpart, no server and no hosting;
everything it does happens locally on the computer and in two files.

## 2. The three parts

The project is a **solution** (the bracket that holds several projects together) with three
**projects** (each a unit that compiles to exactly one result):

```
MagieSoftTimeTracking/
├── src/
│   ├── Zeiterfassung.Core/        — the logic core, without UI
│   └── Zeiterfassung.App/         — the WPF shell, code-behind
├── tests/
│   └── Zeiterfassung.Core.Tests/  — tests, for the core only
├── docs/                          — this documentation
└── Zeiterfassung.slnx             — the solution (new XML format)
```

**The core** contains all of the logic that knows nothing of windows and buttons: the data model
of a time entry, computing the duration, recognising and handling an open entry, reading and
writing the two files, atomic writing, checking the format version. Exactly this part can be
tested automatically.

**The shell** is the WPF interface. It displays, receives clicks and calls the core — nothing
more. It makes no domain decisions; those lie in the core. It is wired up by code-behind.

**The tests** check the core. They do not check the shell.

Dependencies on the outside world follow the same direction: **only the shell has third-party
packages** — LiveCharts2 for the charts in the report and WPF-UI for the look
including switching between light and dark. The core makes do with the standard
library and is meant to stay that way; that keeps it testable and independent of any interface.

The shell also includes `AppearanceSetup` — a small class without a window of its own that binds
the app's colour scheme to the Windows setting: once at start-up and afterwards at every change
while running. It makes no domain decision and therefore belongs in the core just as little as a
window does.

The direction of dependency is sharp and one-sided: the shell may use the core, the tests may use
the core, but the core knows neither shell nor tests.

## 3. Two travelling data files and a path that stays

There are two files that travel and one setting that stays.

| File | Location | Travels? | Owner |
|---|---|---|---|
| `zeiten.json` — the time entries | folder chosen by the user (e.g. OneDrive) | yes, via OneDrive | the app |
| `module.json` — the module list | the same folder | yes, via OneDrive | the user; the app creates it empty if needed and only reads it afterwards |
| `einstellungen.json` — the folder path | locally in `AppData\Local\Zeiterfassung` | **no** | the app, per computer |

Fields and rules of the three files are in `DATENFORMAT.md`.

The reason for the separation: the folder path is different on every computer (`C:\Users\…`). If
it lay in the travelling folder, the PC's path would point into nothing on the notebook. That is
why it lives locally in `AppData\Local` — the branch that Windows keeps per user and per computer
and expressly does not synchronise. The neighbouring branch `AppData\Roaming` would be the wrong
choice: in company and domain environments it travels along with the user profile.

## 4. The flow

> Steps 1 to 3 are drawn as a flowchart in `DIAGRAMME.md`, section B1, steps 4 and 5 as a
> sequence diagram in B3, step 7 in B4 and step 8 in B2.

1. **Every start begins with the check for a second instance.** If the app is already running in
   this Windows session, the second start brings its window to the front and quits without a word;
   no second window appears. Two instances would otherwise work on two independent states of the
   same `zeiten.json`, and the second would overwrite the work of the first.
2. **First start on a computer.** There is no settings file yet. The app opens a folder dialog;
   the user chooses the folder in which the two data files lie (or are to lie). The app remembers
   the path in `AppData\Local\Zeiterfassung`. If the chosen folder is empty, it creates both data
   files empty — `zeiten.json` with an empty list of entries, `module.json` as an empty template
   that the user then fills with module names.
3. **Every further start.** The app reads the path from `AppData`, finds the two files and reads
   them. If the format version does not match, the file is rejected and reported.
4. **Play.** The user has chosen a module and optionally typed an activity. The app immediately
   writes an **open entry** to `zeiten.json`: a new identifier, the start time, the module name in
   plain text, the free text — but no end yet.
5. **Stop.** The app adds the end time and the computed duration to the same entry. The entry is
   thereby complete.
6. **Travelling.** OneDrive mirrors the changed file to the other computer. The app does nothing
   for this.
7. **Reporting.** A button in the main window opens a second, non-modal window with the totals per
   module, week and day as well as the list of entries, optionally over a chosen period. The
   computing happens in the core, the window only displays. From there the entries of
   the chosen period can be written out as a **CSV file** for Excel; the app never reads such a
   file back in.
8. **Start with an open entry.** If the app finds an entry without an end at start-up — after a
   crash, or because the user switched to the other computer without Stop —, it offers both ways:
   complete the entry (enter an end time) or let the clock keep running. The user decides.

## 5. Crash safety

Two precautions keep the data intact:

**The open entry is on disk at once.** Because Play already writes before Stop, a session that has
begun survives a crash. It can be completed afterwards or finished by hand.

**Atomic writing.** Since `zeiten.json` is a single array, the whole file is rewritten at every
change. The dangerous moment is the rewriting itself. That is why the app first writes into a
temporary side file and then renames it in one step. Renaming is indivisible: either the new file
is there in full, or the old one remains intact. A half state cannot arise.

## 5a. What has to come along when switching to the other computer

The data travels via OneDrive (section 3), the program itself does not. For that, the **whole
`publish` folder** is copied, not just the exe: next to it lie the logic core, the chart libraries
and the native drawing library of SkiaSharp. If one of them is missing, the app does not start.

The folder weighs about 50 megabytes in 32 files and is built for **x64**. In addition the
**.NET 10 Desktop Runtime** must be installed on the target computer; it is deliberately not
shipped. The command that produces this folder is in `ENTWICKLUNG.md`, section 3.

## 6. A note on OneDrive

OneDrive synchronises with a delay and, on simultaneous changes, decides by "whoever saves last
wins"; in case of conflict it creates a conflict copy. That is harmless as long as the app is open
on only **one** computer at a time. The user has to keep to this one operating rule; the app does
not enforce it.

## 7. What the app does not do

- **It does not synchronise.** Travelling is done by OneDrive. The app sees only a local file.
- **It sends nothing.** No counterpart, no statistics, no error reporting. What the user records
  stays on their disk.
- **It does not rename or delete a module in use.** Modules can be created and
  maintained in the app, but only as long as no time is booked on them. As soon as an entry runs
  on a module, its name stands in plain text in the time file; the module can then only be
  **deactivated** — it disappears from the selection and remains in the report. `module.json`
  still belongs to the user as well and may be edited by hand; both ways exist side by side.
- **It does not change recorded times afterwards.** The report only displays; corrections are made
  in `zeiten.json` by hand. The app offers no screen for editing or deleting an entry.
- **It reads nothing back that it has written out.** The CSV export only leads outward; reading a
  changed CSV file back in is not intended.

## 8. Constraints

| Question | Decision |
|---|---|
| Runtime environment | Windows desktop, no server |
| Target version | .NET 10 (LTS), target framework `net10.0-windows10.0.19041.0` for the shell |
| Minimum Windows version | Windows 10 version 2004 (May 2020) |
| UI technology | WPF, wired up by code-behind |
| Architecture | solution with three projects (core, shell, tests) |
| Tests | only the UI-free core, framework xUnit |
| Third-party packages | standard library by default; NuGet only with permission. Granted: test framework (tests only), LiveCharts2 and WPF-UI (shell only). The core stays free |
| Data storage | two JSON files in the chosen folder, travelling via OneDrive |
| Path storage | folder path locally in `AppData\Local\Zeiterfassung`, not travelling |
| Time format | ISO 8601, local time with time zone offset, to the second |
| Processor protection CET | switched off for the app — otherwise the Windows dialogs crash |
| Multiple start | prevented per Windows session, the second start raises the existing window |
| Report | totals per module, week and day in a window of its own; the computing happens in the core |
| Shipping | for x64, framework-dependent; about 50 MB in 32 files, the whole folder travels along |
| Output | CSV of the entries, separator and decimal mark from the Windows settings |
| Compact mode | the same window, shrunk and always on top; not remembered |
| Language of the interface | German, including the message texts in the core's exceptions |
| Language of the source code | English, identifiers and comments alike |
| Language of the documentation | German and authoritative; four documents additionally in English under `docs/en/` |
| Licence | MIT |
