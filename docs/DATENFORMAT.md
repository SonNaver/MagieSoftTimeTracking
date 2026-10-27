# Datenformat — MagieSoft TimeTracking

> Vor jedem Zugriff auf die Daten zu lesen. Beschreibt die drei Datendateien und die Regeln,
> nach denen sie gelesen und geschrieben werden — und in Abschnitt 6 die Ausgabedatei für
> Excel, die die App zwar schreibt, aber nie wieder liest.
>
> Dateinamen und Schlüsselnamen sind entschieden: deutsche Dateinamen, weil der Benutzer sie
> im Explorer sieht und von Hand repariert — englische Schlüsselnamen, weil sie der Regel
> „Code englisch" folgen und auch für deutsche Augen transparent sind.

## 1. Die drei Dateien im Überblick

| Datei | Inhalt | Ort | Wandert? |
|---|---|---|---|
| `zeiten.json` | die Zeiteinträge | gewählter Ordner | ja |
| `module.json` | die Modul-Liste, mit Kennzeichen je Modul | gewählter Ordner | ja |
| `einstellungen.json` | Ordnerpfad und Darstellung | lokal in `AppData\Local` | nein |

## 2. `zeiten.json` — die Erfassungsdatei

Die Datei ist **ein einziges Objekt** mit zwei Dingen: einer **Formatversion** im Schlüssel `formatVersion` und einem **Array von Einträgen**. Die Version steht obenauf, damit die App vor dem Deuten prüfen kann, ob die Datei zu ihr passt.

Beim Einlesen wird die Formatversion geprüft. Passt sie nicht oder fehlt sie, wird die ganze Datei abgelehnt und gemeldet — sie wird nie „so gut es geht" gedeutet.

### Die Formatversion

`formatVersion` ist eine ganze Zahl und steht derzeit auf **1**. Geprüft wird auf **exakte Gleichheit**: Jeder andere Wert führt zur Ablehnung, auch ein kleinerer. Es gibt bewusst keine Migration älterer Stände — die wäre Aufwand für einen Fall, den es noch nicht gibt. Entsteht je eine Version 2, wird das Umsteigen dann entschieden.

### Felder eines Eintrags

| Schlüssel | Typ | Pflicht | Bedeutung |
|---|---|---|---|
| `id` | Text (GUID) | ja | Eindeutige Kennung des Eintrags. Eine lange, zufällige, praktisch weltweit einmalige Zeichenfolge. |
| `start` | Zeitstempel | ja | Startzeitpunkt, Datum und Uhrzeit zusammen. Form siehe „Die Zeitform" weiter unten. |
| `end` | Zeitstempel | nein | Endzeitpunkt. **Fehlt dieses Feld (oder ist es leer), ist der Eintrag offen** — er läuft noch oder wurde nie sauber beendet. |
| `module` | Text | ja | Der Modulname **im Klartext**, nicht als Referenz auf die Konfig. |
| `note` | Text | nein | Die Tätigkeit als Freitext. Darf leer sein. |
| `duration` | Dauer | nein | Die gespeicherte Dauer als Text im Muster Stunden:Minuten:Sekunden. Bei einem offenen Eintrag leer. |

### So sieht die Datei aus

Eine Erfassungsdatei mit zwei Einträgen — einem abgeschlossenen und einem offenen. Genau so schreibt die App sie, und genau so muss sie nach einer Handreparatur wieder aussehen:

```json
{
  "formatVersion": 1,
  "entries": [
    {
      "id": "719061f8-d969-41c8-bad2-71ac07034bf4",
      "start": "2026-09-24T14:00:00+02:00",
      "end": "2026-09-24T15:35:00+02:00",
      "module": "Mathematik II",
      "note": "Uebungsblatt 3 gerechnet",
      "duration": "01:35:00"
    },
    {
      "id": "91bee678-8e16-4f56-ad72-24bf8b87d34a",
      "start": "2026-09-24T18:00:00+02:00",
      "module": "Betriebssysteme",
      "note": ""
    }
  ]
}
```

Am zweiten Eintrag ist das Wesentliche abzulesen: **Beim offenen Eintrag fehlen `end` und `duration` ganz** — sie stehen nicht als leere Felder da. Wer von Hand abschließt, fügt beide hinzu; wer nur `end` einträgt und `duration` weglässt, liegt ebenfalls richtig, weil die App die Dauer ohnehin neu rechnet.

### Die Zeitform

Beide Zeitstempel folgen **ISO 8601 als Ortszeit mit angehängtem Zeitzonen-Offset**: zuerst das Datum, dann die Uhrzeit, dann die Abweichung zur Weltzeit — in unserer Zeitzone im Sommer zwei, im Winter eine Stunde. Damit steht die Uhrzeit, die der Benutzer auf seiner Armbanduhr abgelesen hätte, vorne und ist sofort lesbar, während der Offset sie für die Maschine eindeutig macht. Der entscheidende Fall ist die Nacht der Herbst-Zeitumstellung, in der dieselbe Uhrzeit zweimal vorkommt: Ohne Offset ergäbe eine Sitzung über diesen Punkt hinweg eine um eine Stunde falsche Dauer.

**Genauigkeit:** auf die Sekunde. Die Dauer wird ebenso auf Sekunden geführt und in der Oberfläche als Stunden:Minuten:Sekunden angezeigt.

**Fehlender Offset bei Handreparatur:** Trägt der Benutzer beim Reparieren nur Datum und Uhrzeit ein und lässt den Offset weg, wird der Zeitstempel als **Ortszeit dieses Rechners** gedeutet — so, wie er ihn gemeint hat. Beim nächsten Schreiben ergänzt die App den Offset stillschweigend. Das Reparieren bleibt dadurch einfach: Datum und Uhrzeit eintippen genügt. Das ist die eine bewusste Ausnahme von der sonst strengen Regel „im Zweifel ablehnen"; sie betrifft kein mehrdeutiges Format, sondern nur eine fehlende Ergänzung, die die App selbst zuverlässig nachtragen kann.

### Unmögliche Einträge

Ein Eintrag kann nach einer Handreparatur in sich widersprüchlich sein, ohne dass die Datei kaputt wäre: ein Ende **vor** dem Start, ein fehlender Modulname, eine fehlende Kennung. Die App lehnt dann die **ganze Datei** ab und meldet sie — dieselbe Strenge wie bei einer falschen Formatversion.

Die Meldung nennt die **Kennung des betroffenen Eintrags**, damit der Benutzer ihn im Texteditor findet. Nichts geht dabei verloren: Die Datei bleibt unangetastet liegen, bis sie repariert ist.

Der naheliegende sanftere Weg — nur den kaputten Eintrag verwerfen und mit dem Rest weiterarbeiten — ist bewusst verworfen. Die App schriebe die Datei beim nächsten Stop vollständig neu, und der verworfene Eintrag wäre endgültig weg, ohne dass der Benutzer ihn je zu Gesicht bekommen hätte.

Ein Ende, das **genau auf** dem Start liegt, ist dagegen erlaubt. Die Dauer ist dann null — unsinnig, aber nicht widersprüchlich.

### Der offene Eintrag

Es gibt kein eigenes Feld „ist offen". Ein Eintrag ohne Endzeitpunkt **ist** der offene Eintrag. So kann es höchstens einen geben, und der Zustand ist an genau einer Stelle ablesbar.

### Die Dauer-Regel

Die Dauer wird zusätzlich gespeichert, aber **maßgeblich sind immer Start und Ende.**

- Beim Schreiben rechnet die App die Dauer frisch aus `start` und `end` und legt sie mit ab. Die App erzeugt dadurch nie einen Widerspruch.
- Ein Widerspruch kann nur durch Handreparatur entstehen — wenn jemand `start` oder `end` ändert und `duration` daneben stehen lässt.
- Passen beim Einlesen `duration` und (`end` − `start`) nicht zusammen, **überschreibt die App die gespeicherte Dauer stillschweigend** mit dem berechneten Wert. Die Reparatur an den Zeiten setzt sich damit automatisch durch.

## 3. `module.json` — die Modul-Konfig

Ebenfalls **ein Objekt** mit einer **Formatversion** im Schlüssel `formatVersion` und einem **Array von Modulen**. Die Version steht seit dem 26. September 2026 auf **2** und wird auf exakte Gleichheit geprüft.

**Gepflegt wird sie auf zwei Wegen, die nebeneinander bestehen.** Die App legt Module auf Wunsch selbst an und schreibt die Datei dabei vollständig neu; daneben darf der Benutzer sie weiterhin von Hand bearbeiten. Findet die App im gewählten Ordner keine `module.json`, legt sie eine leere Vorlage an — mit korrekter Formatversion und leerem Modul-Array. Solange die Liste leer ist, meldet die App „Noch kein Modul eingetragen" (siehe `OBERFLAECHE.md`, Abschnitt 5).

Jedes Modul ist **ein Objekt mit einem Namensfeld** — kein bloßer Text. So bleibt Platz für spätere Zusatzangaben wie ein Kürzel oder eine Sortierreihenfolge, ohne die Form der Datei umzustellen.

### Zur Formatversion 2

Version 1 kannte je Modul nur den Namen. Mit `isActive` kam ein zweites Feld hinzu, und jede Erweiterung des Formats kostet einen Sprung der Version.

**Anders als bei `einstellungen.json` trifft dieser Sprung beide Rechner.** Die Modul-Konfig wandert über OneDrive: Sobald ein Rechner sie in Version 2 geschrieben hat, lehnt die ältere App-Fassung auf dem anderen sie ab. Die Reihenfolge beim Umstieg lautet deshalb: **erst beide Rechner aktualisieren, dann Module pflegen.** Eine vorhandene Datei der Version 1 wird von Hand auf 2 gehoben — es genügt, die Zahl zu ändern, denn das neue Feld darf fehlen.

**Unbekannte Felder werden nicht geduldet.** Trägt jemand ein Feld ein, das die App nicht kennt, wird die Datei abgelehnt — dieselbe Strenge wie überall sonst. Die Erweiterbarkeit bleibt davon unberührt, kostet aber einen Sprung der Formatversion: Ein neues Feld kommt mit Version 2, nicht nebenbei.

### So sieht die Datei aus

Die App legt die Datei beim ersten Start leer an — nur mit Formatversion und leerer Liste. Gefüllt sieht sie so aus:

```json
{
  "formatVersion": 2,
  "modules": [
    {
      "name": "Mathematik II",
      "isActive": true
    },
    {
      "name": "Betriebssysteme",
      "isActive": false
    },
    {
      "name": "Statistik"
    }
  ]
}
```

Im Beispiel ist „Betriebssysteme" deaktiviert und steht in der Erfassung nicht mehr zur Wahl; seine Zeit zählt in der Auswertung unverändert mit. Bei „Statistik" fehlt das Feld — das ist erlaubt und bedeutet dasselbe wie `true`.

Die kompaktere Schreibweise mit jedem Modul auf einer Zeile ist genauso gültig — JSON ist die Einrückung gleichgültig. Wichtig ist nur, dass jedes Modul **ein Objekt mit `name`** ist und nicht bloß ein Text in Anführungszeichen.

### Regeln für die Modul-Liste

- **Kein Name darf doppelt vorkommen.** Verglichen wird ohne Rücksicht auf Groß- und Kleinschreibung. Der Grund: Der Modulname steht im Klartext in jedem Zeiteintrag und ist dort die einzige Zuordnung — zwei gleichnamige Module wären in den erfassten Daten nicht mehr auseinanderzuhalten. Bei einem Dublett wird die Datei abgelehnt und der betroffene Name gemeldet.
- **Umgebender Leerraum wird entfernt.** Ein versehentliches Leerzeichen am Zeilenende erzeugt sonst einen zweiten, scheinbar gleichnamigen Modulnamen. Das ist die einzige stillschweigende Änderung, die die App am Inhalt dieser Datei vornimmt.
- **Die Reihenfolge bleibt, wie sie dasteht.** Die App sortiert nicht; der Benutzer pflegt die Datei von Hand und findet seine Module in der Auswahl so wieder, wie er sie aufgeschrieben hat.
- **Eine leere Liste ist kein Fehler.** Beim ersten Start ist sie der Normalfall (siehe oben).

| Schlüssel | Typ | Pflicht | Bedeutung |
|---|---|---|---|
| `name` | Text | ja | Der Modulname, wie er in der Auswahl erscheint und in den Zeiteintrag geschrieben wird. |
| `isActive` | Wahrheitswert | nein | Ob das Modul in der Erfassung zur Wahl steht. Fehlt das Feld, gilt `true`. |

### Warum ein benutztes Modul nur deaktiviert wird

Der Modulname steht **im Klartext** in jedem Zeiteintrag und ist dort die einzige Zuordnung. Würde ein Modul umbenannt, hinge jeder alte Eintrag weiter am alten Namen und erschiene in der Auswertung mit dem Vermerk „steht nicht mehr in module.json"; würde es gelöscht, ebenso. Ein Modul, auf das schon Zeit gebucht ist, lässt sich deshalb weder umbenennen noch löschen — nur `isActive` darf auf `false` wechseln. Ein Modul ohne jeden Eintrag ist dagegen frei änderbar, denn an ihm hängt nichts.

Von Hand lässt sich diese Regel umgehen — die App prüft beim Lesen nur das Format, nicht die Historie. Wer in der Datei einen benutzten Modulnamen ändert, trennt seine alten Einträge davon ab; die Auswertung zeigt sie dann als unbekanntes Modul, die Zeit bleibt aber erhalten.

Auch hier wird die Formatversion beim Einlesen geprüft; bei Abweichung wird die Datei abgelehnt.

## 4. `einstellungen.json` — Ordnerpfad und Darstellung

Liegt unter `AppData\Local\Zeiterfassung` und wandert bewusst nicht. Der **Local**-Zweig ist hier wesentlich: `AppData\Roaming` wandert in Firmen- und Domänenumgebungen mit dem Benutzerprofil mit und würde damit genau den Fehler auslösen, den die lokale Ablage verhindern soll: einen Pfad des einen Rechners auf dem anderen.

Die Datei hält zwei Dinge, die beide je Rechner gelten: den Pfad zu dem Ordner, in dem `zeiten.json` und `module.json` liegen, und die gewählte Darstellung.

| Schlüssel | Typ | Pflicht | Bedeutung |
|---|---|---|---|
| `formatVersion` | Ganze Zahl | ja | Die Formatversion, derzeit 2. Geprüft wird auf exakte Gleichheit. |
| `dataFolder` | Text (Pfad) | ja | Der Ordner, in dem die beiden Datendateien liegen. |
| `theme` | Text | nein | Die Darstellung: `system`, `light` oder `dark`. Groß- und Kleinschreibung ist gleichgültig. Fehlt das Feld, gilt `system`. |

```json
{
  "formatVersion": 2,
  "dataFolder": "C:\\Users\\markus\\OneDrive\\Zeiterfassung",
  "theme": "system"
}
```

### Zur Darstellung

`system` heißt: Die App folgt dem App-Modus von Windows und zieht mit, wenn er im laufenden Betrieb umgestellt wird. `light` und `dark` sind feste Wahlen, die von Windows unabhängig sind.

**Das Feld darf fehlen, ein unbekannter Wert wird aber abgelehnt.** Beides hat einen Grund. Eine von Hand geschriebene Datei ohne dieses Feld ist kein Fehler — `system` ist die Voreinstellung und derselbe Zustand, in dem die App vor dieser Formatversion lief. Stünde dort dagegen etwa `dunkel`, wäre das ein Tippfehler, den der Benutzer für eine wirksame Einstellung hielte; die Datei wird deshalb abgelehnt und nennt in der Meldung die erlaubten Werte.

### Zur Formatversion 2

Version 1 kannte nur `dataFolder`. Mit `theme` kam ein zweites inhaltliches Feld hinzu, und nach der Regel, die für alle drei Dateien gilt, kostet jede Erweiterung des Formats einen Sprung der Version — auch dann, wenn das neue Feld freiwillig ist.

**Eine Datei der Version 1 wird deshalb abgelehnt**, wie jede andere unpassende auch. Der Benutzer sieht die gewohnte Meldung im Meldebereich und wählt den Ordner einmalig neu; danach steht die Datei auf Version 2. Weil sie lokal liegt und nicht wandert, betrifft das jeden Rechner genau einmal und unabhängig vom anderen. Ein stillschweigendes Heraufsetzen wurde verworfen — die Strenge gilt für alle drei Dateien gleich, und eine Ausnahme dafür wäre der Anfang vom Ende dieser Regel.

### Ihr Fehlen ist kein Fehler

Hier weicht die Datei von den beiden anderen ab. Ist sie nicht da, ist das der **erste Start auf diesem Rechner**: Die App öffnet den Ordner-Dialog und legt die Datei nach der Auswahl an. Es gibt deshalb keine Meldung und keinen Fehler — der häufigste Weg durch den ersten Start soll kein Fehlerpfad sein.

Ist die Datei dagegen **vorhanden, aber unbrauchbar** — falsche Formatversion, kaputtes JSON, fehlender Ordnerpfad —, gilt dieselbe Strenge wie überall: Sie wird abgelehnt und gemeldet.

**Nicht geprüft wird, ob der gemerkte Ordner noch existiert.** Das Lesen liefert den Pfad, wie er dasteht. Ob der Ordner da ist und die Dateien darin liegen, fällt beim Zugriff auf sie auf — mit den Meldungen aus `OBERFLAECHE.md`, Abschnitt 5, und dem Angebot, den Ordner neu zu wählen. Eine Prüfung beim Lesen wäre ohnehin nur eine Momentaufnahme.

## 5. Lesbarkeit für die Handreparatur

Alle inhaltlichen Felder stehen im Klartext — der Modulname als Text, die Zeitpunkte als lesbare Zeitstempel, der Freitext als Freitext. Nur die Kennung ist eine technische Zeichenfolge; sie wird bei einer Reparatur nicht angefasst, weil man an den lesbaren Feldern repariert. Die Datei ist damit im Ernstfall von Hand zu vervollständigen oder zu korrigieren.

## 6. Die Ausgabedatei für Excel (CSV)

Diese Datei fällt aus dem Rahmen der drei vorigen: Die App **schreibt sie nur und liest sie nie wieder**. Sie hat deshalb keine Formatversion — es gibt niemanden, der sie prüfen müsste. Sie ist eine Momentaufnahme und nimmt an keiner Synchronisierung teil.

**Ort und Name wählt der Benutzer** über einen Speichern-Dialog. Vorgeschlagen wird `zeiterfassung-JJJJ-MM-TT.csv`.

### Aufbau

Eine Kopfzeile, danach eine Zeile je abgeschlossenem Zeiteintrag, **aufsteigend nach Startzeitpunkt** — die älteste Sitzung oben. Ausgegeben wird der Zeitraum, den das Auswertungsfenster gerade zeigt.

| Spalte | Inhalt |
|---|---|
| `Datum` | Der Tag des Beginns, in der Schreibweise des Rechners. |
| `Beginn` | Die Uhrzeit des Beginns, sekundengenau. |
| `Ende` | Die Uhrzeit des Endes, sekundengenau. |
| `Modul` | Der Modulname im Klartext. |
| `Tätigkeit` | Der Freitext; darf leer sein. |
| `Dauer (Stunden)` | Die Dauer als Dezimalzahl mit bis zu vier Nachkommastellen — zum Rechnen in Excel. |
| `Dauer` | Dieselbe Dauer im Muster Stunden:Minuten:Sekunden — zum Lesen. |

**Der offene Eintrag fehlt.** Er hat noch keine Dauer; eine leere Zelle in einer Spalte, die man summiert, wäre eine Falle. Die Meldung nach dem Export sagt, dass einer ausgelassen wurde.

### Kodierung und Zeichen

- **UTF-8 mit Byte-Reihenfolge-Marke.** Das ist der Unterschied zu den Datendateien, die bewusst keine tragen: Ohne die drei Bytes am Anfang liest Excel die Datei als Windows-1252 und macht aus „Tätigkeit" „TÃ¤tigkeit".
- **Trennzeichen und Dezimalzeichen stammen aus den Windows-Einstellungen.** Auf deutschem Windows also Semikolon und Komma — genau das, was das dortige Excel erwartet.
- **Zeilen enden mit Wagenrücklauf und Zeilenvorschub** (CRLF), wie RFC 4180 es vorsieht.
- **Maskiert wird nach RFC 4180.** Enthält ein Feld das Trennzeichen, ein Anführungszeichen oder einen Zeilenumbruch, wird es in Anführungszeichen gefasst; ein Anführungszeichen im Text wird verdoppelt. Das betrifft in der Praxis die Tätigkeit, weil sie Freitext ist.

### So sieht die Datei aus

Drei Einträge auf deutschem Windows, die zweite Tätigkeit mit einem Semikolon darin:

```
Datum;Beginn;Ende;Modul;Tätigkeit;Dauer (Stunden);Dauer
21.09.2026;08:00:00;09:30:00;Mathematik II;Uebungsblatt 3;1,5;01:30:00
22.09.2026;14:00:00;15:00:00;Statistik;"Kapitel 1; Kapitel 2";1;01:00:00
25.09.2026;20:11:56;20:52:25;Betriebssysteme;;0,6747;00:40:29
```

Am dritten Eintrag ist zu sehen, dass eine leere Tätigkeit einfach ein leeres Feld ergibt — zwei Trennzeichen nebeneinander.
