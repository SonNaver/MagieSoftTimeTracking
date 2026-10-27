# MagieSoft TimeTracking

*[English version](README.md)*

Ein kleines Windows-Programm, das festhält, wie viel Zeit auf welches Modul geht. Modul wählen, optional eintippen, woran man gerade sitzt, **Play** drücken — und **Stop**,
wenn man fertig ist. Mehr ist es nicht, und das ist Absicht.

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/hauptfenster-dark.png">
    <img src="docs/screenshots/hauptfenster-light.png" alt="Hauptfenster mit laufender Sitzung" width="345">
  </picture>
</p>

Im Quelltext heißen die Projekte und die Programmdatei *Zeiterfassung*.

## Was es kann

- **Play und Stop.** Eine laufende Sitzung zur Zeit, je Modul, mit optionaler Tätigkeit als
  Freitext.
- **Übersteht Abstürze.** Die Sitzung steht schon beim Play auf der Platte. Geht das Programm oder
  der Rechner vor dem Stop in die Knie, bietet der nächste Start an, den Eintrag abzuschließen
  oder weiterlaufen zu lassen. Jede Datei wird zuerst in eine Nebendatei geschrieben und dann in
  einem Schritt umbenannt — ein Absturz mitten im Schreiben hinterlässt nie eine halbe Datei.
- **Wandert zwischen zwei Rechnern** — ohne eigenen Abgleich. Die Datendateien liegen in einem
  selbst gewählten Ordner, typischerweise in OneDrive, und OneDrive trägt sie hinüber. Der Pfad zu
  diesem Ordner liegt je Rechner in `AppData\Local` und wandert nicht mit.
- **Dateien, die sich von Hand reparieren lassen.** Schlichtes, eingerücktes JSON, der Modulname
  im Klartext in jedem Eintrag, die Zeiten nach ISO 8601 als Ortszeit mit Offset. Das Format
  beschreibt [`docs/DATENFORMAT.md`](docs/DATENFORMAT.md) Feld für Feld.
- **Auswertung.** Summen je Modul, Woche und Tag, aufgeschlüsselt nach Modul mit Durchschnittslinie,
  dazu ein Modulfilter und der Export als CSV für Excel.
- **Hell oder dunkel** oder wie Windows, und ein kompakter Modus, der immer im Vordergrund bleibt.

Was es bewusst **nicht** tut: Es gleicht selbst nichts ab, es sendet nichts nach außen, und es hat
keine Oberfläche zum Bearbeiten erfasster Zeiten — korrigiert wird in der Datei.

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/auswertung-wochen-dark.png">
    <img src="docs/screenshots/auswertung-wochen-light.png" alt="Auswertung: Stunden je Woche, aufgeschlüsselt nach Modul, mit dem Durchschnitt als gestrichelter Linie" width="690">
  </picture>
</p>

## Herunterladen

Eine fertig gebaute Fassung ohne Debug-Dateien gibt es als ZIP unter
[Releases](../../releases). Irgendwohin entpacken und `Zeiterfassung.App.exe` starten; der ganze
Ordner gehört zusammen.

## Voraussetzungen

- Windows 10 ab Version 2004 oder Windows 11, auf **x64**
- Zum Ausführen: die **.NET-10-Desktop-Laufzeitumgebung**
- Zum Bauen: das **.NET-10-SDK** (Visual Studio 2026 ist kein Muss)

## Bauen, prüfen, starten

Aus dem Wurzelverzeichnis:

```bash
dotnet build
dotnet test
dotnet run --project src/Zeiterfassung.App
```

Beim ersten Start fragt das Programm nach einem Ordner für seine Daten; zum Ausprobieren genügt
ein beliebiger leerer Ordner. Einzelheiten in
[`docs/ENTWICKLUNG.md`](docs/ENTWICKLUNG.md).

## Wie es gebaut ist

Drei Projekte: ein Logik-Kern ohne jede Oberfläche und ohne Fremdpakete, eine WPF-Schale, per
Code-Behind angebunden, und ein Testprojekt, das nur den Kern prüft. Die Schale nutzt
[LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2) für die Diagramme und
[WPF-UI](https://github.com/lepoco/wpfui) für das Erscheinungsbild samt hell und dunkel.

## Dokumentation

Die Dokumentation unter [`docs/`](docs/) ist deutsch und maßgeblich.

| Frage | Dokument |
|---|---|
| Wo fange ich an? Bauen, prüfen, welche Klasse wofür | [`ENTWICKLUNG.md`](docs/ENTWICKLUNG.md) |
| Wie ist das Ganze geschnitten, und warum? | [`ARCHITECTURE.md`](docs/ARCHITECTURE.md) |
| Wie sehen die Dateien aus, Feld für Feld? | [`DATENFORMAT.md`](docs/DATENFORMAT.md) |
| Was zeigt die App, und wie verhält sie sich? | [`OBERFLAECHE.md`](docs/OBERFLAECHE.md) |
| Klassen und Abläufe als Diagramme | [`DIAGRAMME.md`](docs/DIAGRAMME.md) |

Vier dieser Dokumente gibt es zusätzlich auf Englisch unter [`docs/en/`](docs/en/). Der Quelltext
ist englisch — Bezeichner wie Kommentare.

## Lizenz

[MIT](LICENSE) © 2026 Markus, MagieSoft. Die Lizenzen der mitgelieferten Fremdbibliotheken
stehen in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
