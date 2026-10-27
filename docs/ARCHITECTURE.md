# Architektur — MagieSoft TimeTracking

> Dieses Dokument beschreibt den aktuellen Stand, nicht seine Herleitung.
>
> **Dasselbe in Diagrammen steht in `DIAGRAMME.md`**: alle 38 Klassen mit ihren Mitgliedern und
> Beziehungen sowie sechs Abläufe, jeder in einem eigenen Diagramm. Dieses Dokument bleibt die
> kürzere Fassung in Prosa; wer es genau wissen will, schlägt dort nach.
>
> **Wer neu dazukommt, fängt mit `ENTWICKLUNG.md` an.** Dort steht, wie man das Projekt baut,
> prüft und ausliefert, welche Klasse wofür zuständig ist und wo man für ein bestimmtes Vorhaben
> ansetzt.

## 1. Einsatzkontext

MagieSoft TimeTracking hält fest, wie viel Zeit der Benutzer je Modul aufwendet. Er wählt ein Modul, tippt optional eine Tätigkeit als Freitext und startet die Zeit mit Play; Stop hält sie an und schließt den Eintrag ab. Benutzer ist eine einzelne Person, die an zwei Rechnern arbeitet — einem PC und einem Notebook.

Die App läuft als gewöhnliches Windows-Programm. Sie hat keine Gegenstelle, keinen Server und kein Hosting; alles, was sie tut, geschieht lokal auf dem Rechner und in zwei Dateien.

## 2. Die drei Teile

Das Projekt ist eine **Solution** (die Klammer, die mehrere Projekte zusammenhält) mit drei **Projekten** (je eine Einheit, die zu genau einem Ergebnis übersetzt wird):

```
MagieSoftTimeTracking/
├── src/
│   ├── Zeiterfassung.Core/        — der Logik-Kern, ohne UI
│   └── Zeiterfassung.App/         — die WPF-Schale, Code-Behind
├── tests/
│   └── Zeiterfassung.Core.Tests/  — Tests, nur für den Kern
├── docs/                          — diese Dokumentation
└── Zeiterfassung.slnx             — die Solution (neues XML-Format)
```

**Der Kern** enthält die gesamte Logik, die nichts von Fenstern und Knöpfen weiß: das Datenmodell eines Zeiteintrags, das Berechnen der Dauer, das Erkennen und Behandeln eines offenen Eintrags, das Lesen und Schreiben der beiden Dateien, das atomare Schreiben, die Prüfung der Formatversion. Genau dieser Teil ist automatisch prüfbar.

**Die Schale** ist die WPF-Oberfläche. Sie zeigt an, nimmt Klicks entgegen und ruft den Kern auf — mehr nicht. Sie trifft keine fachlichen Entscheidungen; die liegen im Kern. Die Anbindung erfolgt per Code-Behind.

**Die Tests** prüfen den Kern. Sie prüfen die Schale nicht.

Die Abhängigkeit nach außen folgt derselben Richtung: **Nur die Schale hat Fremdpakete** — LiveCharts2 für die Diagramme der Auswertung und WPF-UI für das Erscheinungsbild samt Wechsel zwischen heller und dunkler Darstellung. Der Kern kommt mit der Standardbibliothek aus und soll das bleiben; das hält ihn prüfbar und unabhängig von jeder Oberfläche.

Zur Schale gehört dabei auch `AppearanceSetup` — eine kleine Klasse ohne eigenes Fenster, die die Farbstimmung der App an die Einstellung von Windows bindet: einmal beim Start und danach bei jedem Wechsel im laufenden Betrieb. Sie trifft keine fachliche Entscheidung und gehört deshalb ebenso wenig in den Kern wie ein Fenster.

Die Abhängigkeitsrichtung ist scharf und einseitig: Die Schale darf den Kern benutzen, die Tests dürfen den Kern benutzen, aber der Kern kennt weder Schale noch Tests.

## 3. Zwei wandernde Datendateien und ein bleibender Pfad

Es gibt zwei Dateien, die wandern, und eine Einstellung, die bleibt.

| Datei | Ort | Wandert? | Eigentümer |
|---|---|---|---|
| `zeiten.json` — die Zeiteinträge | vom Benutzer gewählter Ordner (z. B. OneDrive) | ja, über OneDrive | die App |
| `module.json` — die Modul-Liste | derselbe Ordner | ja, über OneDrive | der Benutzer; die App legt sie notfalls leer an und liest sie danach nur |
| `einstellungen.json` — der Ordnerpfad | lokal in `AppData\Local\Zeiterfassung` | **nein** | die App, pro Rechner |

Felder und Regeln der drei Dateien stehen in `DATENFORMAT.md`.

Der Grund für die Trennung: Der Ordnerpfad ist auf jedem Rechner ein anderer (`C:\Users\…`). Läge er im wandernden Ordner, zeigte der Pfad des PC auf dem Notebook ins Leere. Deshalb lebt er lokal in `AppData\Local` — dem Zweig, den Windows pro Benutzer und pro Rechner vorhält und ausdrücklich nicht synchronisiert. Der Nachbarzweig `AppData\Roaming` wäre die falsche Wahl: Er wandert in Firmen- und Domänenumgebungen mit dem Benutzerprofil mit.

## 4. Der Ablauf

> Die Punkte 1 bis 3 sind als Flussdiagramm in `DIAGRAMME.md`, Abschnitt B1 gezeichnet, die
> Punkte 4 und 5 als Sequenzdiagramm in B3, Punkt 7 in B4 und Punkt 8 in B2.

1. **Jeder Start beginnt mit der Prüfung auf eine zweite Instanz.** Läuft die App in dieser Windows-Sitzung bereits, holt der zweite Start deren Fenster nach vorn und beendet sich wortlos; es entsteht kein zweites Fenster. Zwei Instanzen arbeiteten sonst auf zwei unabhängigen Ständen derselben `zeiten.json`, und die zweite überschriebe die Arbeit der ersten.
2. **Erster Start auf einem Rechner.** Es gibt noch keine Einstellungsdatei. Die App öffnet einen Ordner-Dialog; der Benutzer wählt den Ordner, in dem die beiden Datendateien liegen (oder liegen sollen). Die App merkt sich den Pfad in `AppData\Local\Zeiterfassung`. Ist der gewählte Ordner leer, legt sie beide Datendateien leer an — `zeiten.json` mit leerer Eintragsliste, `module.json` als leere Vorlage, die der Benutzer anschließend mit seinen Modulnamen füllt.
3. **Jeder weitere Start.** Die App liest den Pfad aus `AppData`, findet die beiden Dateien und liest sie ein. Stimmt die Formatversion nicht, wird die Datei abgelehnt und gemeldet.
4. **Play.** Der Benutzer hat ein Modul gewählt und optional eine Tätigkeit eingetippt. Die App schreibt sofort einen **offenen Eintrag** in `zeiten.json`: eine neue Kennung, der Startzeitpunkt, der Modulname im Klartext, der Freitext — aber noch kein Ende.
5. **Stop.** Die App ergänzt denselben Eintrag um den Endzeitpunkt und die berechnete Dauer. Der Eintrag ist damit abgeschlossen.
6. **Wandern.** OneDrive spiegelt die geänderte Datei auf den anderen Rechner. Die App tut dazu nichts.
7. **Auswerten.** Ein Knopf im Hauptfenster öffnet ein zweites, nicht modales Fenster mit den Summen je Modul, Woche und Tag sowie der Liste der Einträge, wahlweise über einen gewählten Zeitraum. Gerechnet wird im Kern, das Fenster stellt nur dar. Von dort lassen sich die Einträge des gewählten Zeitraums als **CSV-Datei** für Excel ausgeben; die App liest eine solche Datei nie wieder ein.
8. **Start mit offenem Eintrag.** Findet die App beim Start einen Eintrag ohne Ende — nach einem Absturz oder weil ohne Stop zum anderen Rechner gewechselt wurde —, bietet sie beide Wege an: den Eintrag abschließen (Endzeit eingeben) oder die Zeit weiterlaufen lassen. Der Benutzer entscheidet.

## 5. Absturzsicherheit

Zwei Vorkehrungen halten die Daten heil:

**Der offene Eintrag steht sofort auf der Platte.** Weil Play schon vor dem Stop schreibt, überlebt eine begonnene Sitzung einen Absturz. Sie kann anschließend abgeschlossen oder von Hand vervollständigt werden.

**Atomares Schreiben.** Da `zeiten.json` ein einziges Array ist, wird bei jeder Änderung die ganze Datei neu geschrieben. Der gefährliche Moment ist das Neuschreiben selbst. Deshalb schreibt die App zuerst in eine temporäre Nebendatei und benennt diese dann in einem Schritt um. Das Umbenennen ist unteilbar: Entweder ist die neue Datei vollständig da, oder die alte bleibt unversehrt. Ein halber Zustand kann nicht entstehen.

## 5a. Was beim Wechsel auf den anderen Rechner mitmuss

Die Daten wandern über OneDrive (Abschnitt 3), das Programm selbst nicht. Kopiert wird dafür der **ganze `publish`-Ordner**, nicht nur die exe: Neben ihr liegen der Logik-Kern, die Diagramm-Bibliotheken und die native Zeichenbibliothek von SkiaSharp. Fehlt eine davon, startet die App nicht.

Der Ordner wiegt rund 50 Megabyte in 32 Dateien und ist für **x64** gebaut. Auf dem Zielrechner muss außerdem die **.NET-10-Desktop-Laufzeitumgebung** installiert sein; sie wird bewusst nicht mitgeliefert. Der Befehl, der diesen Ordner erzeugt, steht in `ENTWICKLUNG.md`, Abschnitt 3.

## 6. Ein Hinweis zu OneDrive

OneDrive synchronisiert mit Verzögerung und entscheidet bei zeitgleichen Änderungen nach „wer zuletzt speichert, gewinnt"; im Konfliktfall legt es eine Konfliktkopie an. Das ist harmlos, solange die App immer nur auf **einem** Rechner gleichzeitig geöffnet ist. Diese eine Betriebsregel muss der Benutzer einhalten; die App erzwingt sie nicht.

## 7. Was die App nicht tut

- **Sie synchronisiert nicht.** Das Wandern erledigt OneDrive. Die App sieht nur eine lokale Datei.
- **Sie sendet nichts.** Keine Gegenstelle, keine Statistik, keine Fehlerübermittlung. Was der Benutzer erfasst, bleibt auf seiner Platte.
- **Sie benennt ein benutztes Modul nicht um und löscht es nicht.** Module lassen sich in der App anlegen und pflegen, aber nur solange auf ihnen keine Zeit gebucht ist. Sobald ein Eintrag auf ein Modul läuft, steht dessen Name im Klartext in der Erfassungsdatei; das Modul kann dann nur noch **deaktiviert** werden — es verschwindet aus der Auswahl und bleibt in der Auswertung. `module.json` gehört weiterhin auch dem Benutzer und darf von Hand bearbeitet werden; beide Wege bestehen nebeneinander.
- **Sie ändert erfasste Zeiten nicht nachträglich.** Die Auswertung zeigt nur an; korrigiert wird in `zeiten.json` von Hand. Die App bietet keine Oberfläche zum Bearbeiten oder Löschen eines Eintrags.
- **Sie liest nichts ein, was sie ausgegeben hat.** Der CSV-Export führt nur hinaus; eine geänderte CSV-Datei wieder einzulesen ist nicht vorgesehen.

## 8. Rahmenbedingungen

| Frage | Festlegung |
|---|---|
| Laufzeitumgebung | Windows-Desktop, kein Server |
| Zielversion | .NET 10 (LTS), Zielplattform `net10.0-windows10.0.19041.0` für die Schale |
| Windows-Mindestversion | Windows 10 ab Version 2004 (Mai 2020) |
| UI-Technik | WPF, Anbindung per Code-Behind |
| Architektur | Solution mit drei Projekten (Kern, Schale, Tests) |
| Tests | nur der UI-freie Kern, Framework xUnit |
| Fremdpakete | Standardbibliothek als Default; NuGet nur mit Erlaubnis. Erteilt: Testframework (nur Tests), LiveCharts2 und WPF-UI (nur Schale). Der Kern bleibt frei |
| Datenablage | zwei JSON-Dateien im gewählten Ordner, wandernd über OneDrive |
| Pfadablage | Ordnerpfad lokal in `AppData\Local\Zeiterfassung`, nicht wandernd |
| Zeitform | ISO 8601, Ortszeit mit Zeitzonen-Offset, sekundengenau |
| Prozessorschutz CET | für die App abgeschaltet — sonst stürzen die Windows-Dialoge ab |
| Mehrfachstart | unterbunden je Windows-Sitzung, der zweite Start hebt das vorhandene Fenster |
| Auswertung | Summen je Modul, Woche und Tag in einem eigenen Fenster; gerechnet wird im Kern |
| Auslieferung | für x64, rahmenabhängig; rund 50 MB in 32 Dateien, der ganze Ordner wandert mit |
| Ausgabe | CSV der Einträge, Trennzeichen und Dezimalzeichen aus den Windows-Einstellungen |
| Kompaktmodus | dasselbe Fenster, zusammengezogen und immer im Vordergrund; wird nicht gemerkt |
| Sprache der Oberfläche | Deutsch, samt der Meldungstexte in den Ausnahmen des Kerns |
| Sprache des Quelltextes | Englisch, Bezeichner wie Kommentare |
| Sprache der Dokumentation | Deutsch und maßgeblich; vier Dokumente zusätzlich englisch unter `docs/en/` |
| Lizenz | MIT |
