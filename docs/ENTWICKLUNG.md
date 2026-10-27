# Entwicklung — MagieSoft TimeTracking

> **Das Dokument für den Einstieg.** Es beantwortet vier Fragen: Was soll das Programm? Wie ist es
> aufgebaut? Wie baue und prüfe ich es? Und wo finde ich, was ich suche?
>
> Es wiederholt die übrigen Dokumente nicht, sondern verweist auf sie — zwei Fassungen desselben
> Sachverhalts driften mit der Zeit auseinander. Der Wegweiser in Abschnitt 7 sagt, welches
> Dokument welche Frage beantwortet.
>
> Alle Befehle in Abschnitt 3 sind ausgeführt worden, bevor sie hier hineingeschrieben wurden.
> Die Zahlen daneben stammen aus diesen Läufen.

## 1. Was das Programm soll

MagieSoft TimeTracking hält fest, wie viel Zeit auf welches Modul geht. Der
Benutzer wählt ein Modul, tippt optional eine Tätigkeit als Freitext und startet mit **Play** die
Zeit; **Stop** hält sie an und schließt den Eintrag ab. Das ist der ganze Kern — bewusst „ganz
simple".

Drei Dinge prägen darüber hinaus jede Entscheidung im Code:

**Die Erfassung wandert zwischen zwei Rechnern**, einem PC und einem Notebook. Dafür gibt es
keinen eigenen Mechanismus: Die Datendateien liegen in einem Ordner, den OneDrive synchronisiert.
Für die App ist das schlicht „eine Datei, die schon da ist".

**Absturzsicherheit geht vor Bequemlichkeit.** Stürzt das Programm zwischen Play und Stop ab, darf
keine begonnene Sitzung verloren gehen. Deshalb steht der Eintrag schon beim Play auf der Platte,
und deshalb wird jede Datei über eine Nebendatei geschrieben und dann umbenannt.

**Die Dateien müssen sich von Hand reparieren lassen.** Sie sind eingerücktes JSON, die
Modulnamen stehen im Klartext darin, und die Dauer ist redundant mitgeschrieben, damit ein Mensch
sie lesen kann. Diese Annahme trägt eine ganze Reihe von Entscheidungen — unter anderem die, dass
die App **keine** Oberfläche zum Bearbeiten erfasster Zeiten hat.

Was das Programm ausdrücklich **nicht** tut, damit niemand danach sucht: Es synchronisiert nicht
(das macht OneDrive), es sendet nichts nach außen, es ändert erfasste Zeiten nicht nachträglich,
es liest keine CSV-Datei ein, und es benennt ein Modul nicht um, auf das bereits Zeit gebucht ist.
Die Begründungen stehen in `ARCHITECTURE.md`, Abschnitt 7.

## 2. Die Solution auf einen Blick

Drei Projekte, zusammengehalten von `Zeiterfassung.slnx` (dem neuen XML-Format für Solutions).

| Projekt | Zielplattform | Was darin liegt | Fremdpakete |
|---|---|---|---|
| `Zeiterfassung.Core` | `net10.0` | die gesamte Logik, ohne jede Oberfläche | **keine** |
| `Zeiterfassung.App` | `net10.0-windows10.0.19041.0` | die WPF-Schale, per Code-Behind angebunden | LiveCharts2, WPF-UI |
| `Zeiterfassung.Core.Tests` | `net10.0` | 169 Tests, nur für den Kern | xUnit |

**Die Abhängigkeit läuft in eine Richtung:** Schale → Kern und Tests → Kern. Der Kern kennt weder
Schale noch Tests. Das ist keine Stilfrage, sondern der Grund, warum sich die Logik ohne Fenster
prüfen lässt.

**Nur die Schale hat Fremdpakete**, und beide sind einzeln erlaubt worden: LiveCharts2 für die
Diagramme der Auswertung und WPF-UI für das Erscheinungsbild samt hell/dunkel.
**Der Kern ist frei von Fremdpaketen und soll es bleiben.** Vor jedem weiteren Paket steht
eine bewusste Abwägung, ob die Standardbibliothek wirklich nicht reicht.

```
MagieSoftTimeTracking/
├── Zeiterfassung.slnx
├── README.md · README.de.md          die Startseite auf GitHub, englisch und deutsch
├── LICENSE                           MIT
├── THIRD-PARTY-NOTICES.txt           die Lizenzen der mitgelieferten Fremdbibliotheken
├── src/
│   ├── Zeiterfassung.Core/
│   │   ├── Model/            TimeEntry, StudyModule, StudyModuleList, ModuleUsage,
│   │   │                     AppSettings, ThemePreference, DurationFormat
│   │   ├── Storage/          TimeEntryFile, ModuleFile, SettingsFile, DataFileSerializer,
│   │   │                     AtomicFileWriter, DataFileException, fünf Stored…-Typen
│   │   ├── Workflow/         Workspace, TimeTracker
│   │   └── Reporting/        TimeReport, DateRange, ModuleFilter, ModuleTotal, PeriodTotal, CsvExport
│   └── Zeiterfassung.App/
│       ├── App.xaml(.cs)             Start, Einzelinstanz, Darstellung
│       ├── MainWindow.xaml(.cs)      das Hauptfenster
│       ├── ReportWindow.xaml(.cs)    die Auswertung
│       ├── OpenEntryDialog.xaml(.cs) der Dialog beim offenen Eintrag
│       ├── ModuleDialog.xaml(.cs)    die Modulpflege
│       ├── AppearanceSetup.cs        hell/dunkel
│       ├── SingleInstanceGuard.cs    die Sperre gegen den zweiten Start
│       ├── ModuleRow / PeriodRow / EntryRow / ModuleEditRow.cs   Anzeigezeilen
│       └── app.manifest
├── tests/
│   └── Zeiterfassung.Core.Tests/     Model/, Storage/, Workflow/, Reporting/
└── docs/                             diese Dokumentation
    ├── en/                           vier Dokumente zusätzlich auf Englisch
    └── screenshots/                  die Bilder der README, mit Demodaten aufgenommen
```

## 3. Einrichten, bauen, prüfen, ausliefern

**Voraussetzungen.** Windows 10 ab Version 2004 oder Windows 11, und das **.NET-10-SDK**. Visual
Studio 2026 ist die Entwicklungsumgebung, aber nichts hier verlangt sie — alle Befehle laufen in
jeder Konsole. Dass das SDK da ist, zeigt `dotnet --version`; erwartet wird 10.x (geprüft mit
10.0.301).

**Bauen.** Aus dem Wurzelverzeichnis, ohne Argumente — die Solution wird gefunden:

```bash
dotnet build
```

Baut alle drei Projekte. Beim ersten Mal lädt NuGet die Pakete; danach dauert es rund sechs
Sekunden. **Erwartet werden null Warnungen und null Fehler** — der Stand ist warnungsfrei, und das
soll er bleiben.

**Prüfen.** Ebenfalls aus dem Wurzelverzeichnis:

```bash
dotnet test
```

Läuft in unter einer Sekunde durch, **169 Tests, alle bestanden**. Geprüft wird ausschließlich der
Kern; die WPF-Oberfläche wird nicht automatisch getestet, weil dort der Aufwand hoch und der
Nutzen gering ist. Was das für den eigenen Beitrag heißt, steht in Abschnitt 5.

**Starten.** Zum Ausprobieren während der Arbeit:

```bash
dotnet run --project src/Zeiterfassung.App
```

Beim allerersten Start fragt die App nach einem Ordner für ihre Daten. Zum Entwickeln genügt ein
beliebiger leerer Ordner — er muss nicht in OneDrive liegen. Der gewählte Pfad landet in
`%LOCALAPPDATA%\Zeiterfassung\einstellungen.json`; diese Datei zu löschen, setzt die App auf den
Zustand „erster Start" zurück.

**Ausliefern.** Die Architekturangabe gehört zwingend dazu:

```bash
dotnet publish src/Zeiterfassung.App -c Release -r win-x64 --self-contained false -o publish
```

Ergebnis: **32 Dateien, 50,4 MB** in `publish/` (das Verzeichnis ist in `.gitignore`). Ohne
`-r win-x64` käme die native Zeichenbibliothek von SkiaSharp für drei Architekturen zugleich mit
und der Ordner wüchse deutlich. `--self-contained false` heißt, dass die .NET-Laufzeitumgebung
**nicht** mitgeliefert wird; auf dem Zielrechner muss die **.NET-10-Desktop-Laufzeitumgebung**
installiert sein.

**Auf den anderen Rechner kommt der ganze Ordner**, nicht nur die exe: Daneben liegen der
Logik-Kern, die Diagramm-Bibliotheken und die native Zeichenbibliothek. Fehlt eine davon, startet
die App nicht.

## 4. Zwei Zeilen, die nicht aufgeräumt werden dürfen

Beide stehen in `src/Zeiterfassung.App/Zeiterfassung.App.csproj`, beide sehen nach Altlast aus,
beide sind keine. In der Projektdatei steht über jeder ein Kommentar — hier stehen sie noch
einmal, weil das Aufräumen genau dann passiert, wenn man die Datei nicht aufmerksam liest.

**`<CETCompat>false</CETCompat>`** schaltet den Schutz des Prozessors gegen Rücksprung-Angriffe
für dieses Programm ab. Ohne diese Zeile stürzen die Datei- und Ordnerdialoge von Windows ab — und
zwar nur auf manchen Rechnern, was die Suche danach besonders unangenehm macht.

**`<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>`** nennt die Windows-Version
ausdrücklich. Das kürzere `net10.0-windows` bedeutet stillschweigend Windows 7; NuGet fällt dann
für SkiaSharp auf die .NET-Framework-Fassung samt OpenTK 3 zurück. Der Preis dieser Angabe ist die
Mindestanforderung Windows 10 ab Version 2004.

## 5. Wo finde ich was

Nach Vorhaben statt nach Datei. Die letzte Spalte ist keine Höflichkeit: Die Dokumentation gehört
zur Änderung, nicht dahinter.

| Vorhaben | Anzufassen | Mitzuziehen |
|---|---|---|
| Eine neue Auswertung | `Reporting/TimeReport.cs` (rechnen), `ReportWindow.xaml(.cs)` (zeigen) | `ARCHITECTURE.md` 4, `OBERFLAECHE.md` 6, `DIAGRAMME.md` A4/B4 |
| Ein Feld in `zeiten.json` | `Storage/StoredTimeEntry.cs`, `Storage/TimeEntryFile.cs` (**`SupportedFormatVersion` hochsetzen**), `Model/TimeEntry.cs` | `DATENFORMAT.md` 2, `DIAGRAMME.md` A3 |
| Ein Feld in `module.json` oder `einstellungen.json` | der jeweilige `Stored…`-Typ und die Dateiklasse (**Version hochsetzen**) | `DATENFORMAT.md` 3 bzw. 4 |
| Ein neues Fenster | neue `*.xaml(.cs)` in der Schale, von `FluentWindow` erbend, bei `AppearanceSetup.RegisterWindow` anmelden | `OBERFLAECHE.md`, `DIAGRAMME.md` A5 |
| Eine Meldung ändern | `MainWindow.ShowStatus` / `ShowProblem`, sonst die Meldung in der werfenden Kernklasse | `OBERFLAECHE.md` 5 |
| Einen Knopf sperren oder freigeben | `UpdateControlState` und `UpdateToolTips` im jeweiligen Fenster — **nur dort** | `OBERFLAECHE.md` |
| Etwas an der Uhr oder der Dauer | `Model/TimeEntry.cs`, `Workflow/TimeTracker.cs` | `DATENFORMAT.md` 2 |
| Etwas am Schreiben auf die Platte | `Storage/AtomicFileWriter.cs` — und sehr genau `ARCHITECTURE.md`, Abschnitt 5 lesen | `ARCHITECTURE.md` 5 |
| Ein NuGet-Paket aufnehmen | nur in der Schale, `*.csproj` | `ENTWICKLUNG.md` 2, `ARCHITECTURE.md` 8 |
| Eine neue Fassung veröffentlichen | — | `docs/en/` (Stand-Vermerk), `docs/screenshots/`, `README.md` und `README.de.md` |

**Drei Regeln, die bei fast jeder Änderung greifen:**

**Fachliches gehört in den Kern, nicht in ein Fenster.** Wenn eine Änderung in der Schale eine
Entscheidung trifft — rechnet, prüft, deutet —, gehört sie eine Ebene tiefer. Sonst ist sie nicht
prüfbar.

**Was in den Kern kommt, bekommt Tests.** Das Testprojekt bildet die Ordnerstruktur des Kerns ab;
die Prüfung für `Reporting/TimeReport.cs` liegt in `tests/…/Reporting/TimeReportTests.cs`. Die
Namensform ist `Methode_Ausgangslage_Erwartung`.

**Ein geändertes Dateiformat kostet eine Formatversion.** Der JSON-Leser lehnt unbekannte Felder
ab, damit genau das nicht stillschweigend geschehen kann. Wer ein Feld hinzufügt und die Version
stehen lässt, macht die Datei auf dem anderen Rechner unlesbar — und weil `module.json` **wandert**,
müssen beide Rechner die neue Fassung haben, bevor dort etwas gepflegt wird.

## 6. Klasse zu Aufgabe

Eine Zeile je Klasse. Die Mitglieder und Beziehungen stehen in `DIAGRAMME.md`, Teil A.

### Kern — `Model`: die Fachbegriffe

| Klasse | Aufgabe |
|---|---|
| `TimeEntry` | Ein Zeiteintrag. Ohne Endzeitpunkt ist er der offene Eintrag; die Dauer wird stets frisch gerechnet |
| `StudyModule` | Ein Modul: Name und ob es zur Wahl steht |
| `StudyModuleList` | Die Modul-Liste; wacht darüber, dass kein Name doppelt vorkommt |
| `ModuleUsage` | Beantwortet die eine Frage: Ist auf dieses Modul schon Zeit gebucht? |
| `AppSettings` | Die Einstellungen eines Rechners: Datenordner und Darstellung; unveränderlich |
| `ThemePreference` | Die drei Möglichkeiten der Darstellung: System, Hell, Dunkel |
| `DurationFormat` | Formt eine Zeitspanne zu `HH:mm:ss` — die einzige Stelle, die das tut |

### Kern — `Storage`: die Übersetzung zur Platte

| Klasse | Aufgabe |
|---|---|
| `TimeEntryFile` | Liest und schreibt `zeiten.json` (Formatversion 1) |
| `ModuleFile` | Liest und schreibt `module.json` (Formatversion 2) |
| `SettingsFile` | Liest und schreibt `einstellungen.json` in `AppData\Local` (Formatversion 2) |
| `DataFileSerializer` | Die gemeinsamen JSON-Einstellungen und die Prüfung der Formatversion |
| `AtomicFileWriter` | Schreibt über eine Nebendatei und benennt sie um — die Absturzsicherung |
| `DataFileException` | Die einzige Ausnahme, die dieser Bereich nach außen lässt, mit Meldung in ganzen Sätzen |
| `StoredTimeFile` · `StoredTimeEntry` | Das Abbild von `zeiten.json`, wie sie wirklich aussieht — alle Felder nullbar |
| `StoredModuleFile` · `StoredModule` | Dasselbe für `module.json` |
| `StoredSettingsFile` | Dasselbe für `einstellungen.json` |

### Kern — `Workflow`: der Betrieb

| Klasse | Aufgabe |
|---|---|
| `Workspace` | Der gewählte Datenordner als Objekt; kennt als einzige Klasse die beiden Dateinamen |
| `TimeTracker` | Führt die Sitzung: starten, stoppen, offenen Eintrag finden, fortsetzen, abschließen |

### Kern — `Reporting`: das Rechnen

| Klasse | Aufgabe |
|---|---|
| `TimeReport` | Die vier Auswertungen: je Modul, je Woche, je Tag, und die Liste der Einträge; dazu der Durchschnitt über eine Folge von Zeiträumen |
| `DateRange` | Ein Zeitraum; entscheidet anhand des **Starttags**, ob ein Eintrag dazugehört |
| `ModuleFilter` | Die Auswahl der Module, auf die beschränkt wird; leer heißt „alle" |
| `ModuleTotal` | Das Ergebnis je Modul: Summe, Anzahl, und ob das Modul noch in der Liste steht |
| `PeriodTotal` | Das Ergebnis je Woche oder Tag: Summe, Anzahl und die Aufschlüsselung je Modul |
| `CsvExport` | Baut die CSV-Datei; Trennzeichen und Dezimalzeichen kommen von Windows |

### Schale

| Klasse | Aufgabe |
|---|---|
| `App` | Der Start: zweite Instanz abwehren, Darstellung anwenden, Hauptfenster öffnen |
| `SingleInstanceGuard` | Das benannte Sperrobjekt je Windows-Sitzung; holt sonst das vorhandene Fenster nach vorn |
| `AppearanceSetup` | Wendet hell oder dunkel an und meldet die Fenster bei Windows an — oder ausdrücklich ab |
| `MainWindow` | Das Hauptfenster: Ordnerwahl, Modulauswahl, Play und Stop, Uhr, Kompaktmodus, Menüs |
| `OpenEntryDialog` | Fragt beim offenen Eintrag: abschließen oder weiterlaufen lassen |
| `OpenEntryChoice` | Die Antwort dieses Dialogs: unentschieden, abschließen, fortsetzen |
| `ModuleDialog` | Module anlegen, umbenennen, löschen, deaktivieren — je nachdem, was erlaubt ist |
| `ReportWindow` | Die Auswertung: vier Tabellen, drei Diagramme, Zeitraum, Modulfilter, CSV-Export; nicht modal |
| `ModuleRow` · `PeriodRow` · `EntryRow` | Fertig formatierte Anzeigezeilen für die drei Tabellen der Auswertung |
| `ModuleEditRow` | Dasselbe für die Tabelle der Modulpflege, samt „Zeit gebucht?" |

### Tests

169 Testfälle in zwölf Klassen, dazu ein Helfer. Die Ordnerstruktur bildet die des Kerns ab.

| Klasse | Was sie prüft |
|---|---|
| `TimeEntryTests` | Anlegen, Abschließen, Wiederherstellen, die Dauer — auch über die Zeitumstellung hinweg |
| `StudyModuleTests` · `StudyModuleListTests` | Namen und ihre Eindeutigkeit, auch bei abweichender Groß- und Kleinschreibung |
| `ModuleUsageTests` | Wann ein Modul noch bearbeitet werden darf |
| `TimeEntryFileTests` | Lesen und Schreiben von `zeiten.json`, Formatversion, beschädigte Einträge, keine Reste |
| `ModuleFileTests` · `SettingsFileTests` | Dasselbe für die beiden anderen Dateien, samt unbekannter Felder und Werte |
| `WorkspaceTests` | Erster Start, gemerkter Ordner, verschwundener Ordner, fehlende Dateien |
| `TimeTrackerTests` | Start und Stopp, der offene Eintrag, und was passiert, wenn die Datei nebenher von Hand geändert wird |
| `TimeReportTests` | Die vier Auswertungen, Lücken zwischen Zeiträumen, Mitternacht, Jahreswechsel, Modulfilter, Aufschlüsselung je Modul, Durchschnitt |
| `DateRangeTests` | Die Grenzen eines Zeitraums, offen nach vorn wie nach hinten |
| `CsvExportTests` | Trennzeichen, Anführungszeichen, Zeilenumbrüche im Freitext, ausgelassene offene Einträge, Modulfilter |
| `StoppedClock` | Kein Test, sondern eine gestellte Uhr: Damit lassen sich Dauern prüfen, ohne zu warten |

## 7. Wegweiser in die übrigen Dokumente

| Frage | Dokument |
|---|---|
| Wie ist das Ganze geschnitten, und warum? | `ARCHITECTURE.md` |
| Wie sehen die drei Dateien aus, Feld für Feld? | `DATENFORMAT.md` — **vor jedem Zugriff auf die Daten zu lesen** |
| Was zeigt die App, und wie verhält sie sich? | `OBERFLAECHE.md` |
| Wie hängen die Klassen zusammen, wie laufen die Abläufe? | `DIAGRAMME.md` |
| Gibt es das auch auf Englisch? | `docs/en/` — dieses Dokument, `ARCHITECTURE.md`, `DATENFORMAT.md` und `DIAGRAMME.md`, jedes mit Stand-Vermerk |

**Für den ersten Tag in dieser Reihenfolge:** dieses Dokument, dann `ARCHITECTURE.md` für das
große Bild, dann `DATENFORMAT.md` — weil fast jede Änderung früher oder später an den Dateien
landet. `DIAGRAMME.md` ist zum Nachschlagen gedacht, nicht zum Durchlesen.
