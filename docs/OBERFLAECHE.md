# Oberfläche — MagieSoft TimeTracking

> Beschreibt, was die App zeigt und wie sie sich verhält. Kein Layout im Detail — das entsteht
> beim Bauen —, sondern die Zustände und das Verhalten, aus denen das Layout folgt.
>
> Dieses Dokument ist bewusst schlank gehalten und wird beim Bauen der Schale verfeinert.

## 0. Wie die App aussieht

Die Oberfläche folgt der Fluent-Gestaltung von Windows 11. Drei Regeln gelten durchgehend:

**Hell oder dunkel wählt der Benutzer — oder er überlässt es Windows.** In der Titelleiste des Hauptfensters sitzt rechts ein Knopf, der ein Menü mit drei Einträgen aufklappt: *Wie das System*, *Immer hell* und *Immer dunkel*. Ein Häkchen zeigt, was gerade gilt.

Die Wahl wirkt sofort auf alle offenen Fenster und wird in `einstellungen.json` gemerkt, sodass sie den nächsten Start überdauert. Sie gilt **je Rechner**: Die Datei wandert nicht, PC und Notebook dürfen also verschieden eingestellt sein.

Bei *Wie das System* — der Voreinstellung — liest die App beim Start, was in den Windows-Einstellungen unter „App-Modus" steht, und zieht mit, wenn das im laufenden Betrieb umgestellt wird: Fenster, Meldebereich und auch die Diagramme der Auswertung. Bei einer festen Wahl bleibt sie dagegen hell beziehungsweise dunkel, egal was Windows tut. Alle drei Fenster zeichnen ihre Titelleiste selbst und färben sie deshalb mit.

**Die Wahl wird nur gemerkt, wenn schon ein Datenordner gewählt ist.** Ohne ihn gibt es keine Einstellungsdatei, in die sie gehörte; die Umschaltung wirkt dann für diese Sitzung und ist beim nächsten Start wieder die alte. Das ist verschmerzbar, denn ohne Datenordner lässt sich ohnehin keine Zeit erfassen.

**Symbole gibt es dort, wo sie eindeutig sind.** Play und Stop tragen **nur** ein Symbol: Dreieck und Quadrat sind seit Jahrzehnten dieselben, und die beiden runden Knöpfe zeigen ohne ein Wort, was die Hauptbedienung ist. Alle übrigen Knöpfe — *Auswertung…*, *Kompakt*, *Ordner wählen…*, *Nach CSV…*, *Alles*, *Aktualisieren* — tragen **Symbol und Text**, weil ein Diagramm- oder Ordnersymbol allein mehrdeutig bliebe. Die Knöpfe im Dialog der offenen Sitzung tragen **nur Text**: Dort ist eine Entscheidung zu treffen, die gelesen werden soll.

**Jedes Bedienelement hat einen Tooltip — auch ein gesperrtes.** Das ist der eigentliche Zweck: Ein graues Play sagt von sich aus nicht, warum es grau ist. Der Tooltip sagt es, und zwar in der Form „Gesperrt: Zuerst ein Modul wählen." Dasselbe gilt für die während einer laufenden Sitzung gesperrte Modulauswahl und das Tätigkeitsfeld.

## 1. Das Hauptfenster

Ein einziges Fenster. Es enthält:

- **eine Modul-Auswahl** — eine Auswahlliste, gefüllt aus `module.json`. Sie zeigt nur die Module, die **zur Wahl stehen**; deaktivierte erscheinen hier nicht (siehe Abschnitt 1a). Gepflegt wird die Liste über das Bücherregal-Symbol in der Titelleiste.
- **ein Textfeld für die Tätigkeit** — Freitext, darf leer bleiben. Solange es leer ist, steht der Hinweis „Tätigkeit (darf leer bleiben)" blass im Feld selbst; eine eigene Beschriftung darüber braucht es deshalb nicht.
- **einen Knopf Play** und **einen Knopf Stop** — zwei runde Symbolknöpfe, Play in der Akzentfarbe des Systems (siehe Abschnitt 0).
- **eine laufende Zeitanzeige**, solange ein Eintrag läuft.
- **einen Meldebereich** für Statusmeldungen und Fehler — ein abgesetzter Kasten im unteren Teil des Fensters (siehe Abschnitt 5).

Es gibt zwei Grundzustände: **kein Eintrag läuft** (Play ist bedienbar, Stop nicht) und **ein Eintrag läuft** (Stop ist bedienbar, Play nicht; die laufende Zeit wird angezeigt).

Das Fenster ist **mindestens 600 Pixel hoch**. Der Grund steht in Abschnitt 5: Der Meldebereich muss auch eine mehrzeilige Meldung samt Dateipfad und Knopf vollständig zeigen können, ohne sich über die Knöpfe darüber zu schieben.

## 1a. Der Modul-Dialog

Das Bücherregal-Symbol in der Titelleiste, links neben der Wahl der Darstellung, öffnet ein eigenes, **modales** Fenster zur Pflege der Modul-Liste. Modal, weil es `module.json` schreibt und daneben nichts an derselben Liste gedreht werden soll — anders als das Auswertungsfenster, das nur liest.

Das Fenster zeigt alle Module, auch die deaktivierten, mit zwei Angaben: ob sie **zur Wahl stehen** und ob auf ihnen schon **Zeit gebucht** ist. Darunter ein Namensfeld und vier Knöpfe.

**Was möglich ist, hängt davon ab, ob schon Zeit auf dem Modul liegt:**

- **Kein einziger Eintrag** — das Modul lässt sich **umbenennen** und **löschen**. An ihm hängt nichts.
- **Mindestens ein Eintrag** — das Modul lässt sich nur noch **deaktivieren**. Es verschwindet dann aus der Auswahl der Erfassung, bleibt aber in der Datei, und seine Zeit zählt in der Auswertung unverändert mit. Ein deaktiviertes Modul lässt sich jederzeit wieder **aktivieren**.

Der Grund für diese Trennung steht in `DATENFORMAT.md`, Abschnitt 3: Der Modulname steht im Klartext in jedem Zeiteintrag. Eine Umbenennung träfe die alten Einträge nicht mit und risse sie von ihrem Modul los.

**Gelöscht wird nur nach Rückfrage** — eine der wenigen Ausnahmen von der Regel aus Abschnitt 5, dass Meldungen in den Meldebereich gehören. Hier ist eine Antwort nötig, bevor es weitergeht.

**Jede Änderung wird sofort geschrieben**, nicht erst beim Schließen. Das passt zur Absturzsicherheit und erspart dem Benutzer einen Zustand „geändert, aber noch nicht gespeichert". Eine Meldung am unteren Rand sagt jeweils, was geschehen ist.

**Lässt sich `module.json` nicht lesen**, bleibt der Dialog schreibgeschützt: Alle vier Knöpfe sind gesperrt, und die Meldung sagt, warum. Andernfalls schriebe ein Anlegen die leere Liste zurück, und die Module der unlesbaren Datei wären verloren.

**Der Knopf ist gesperrt, während eine Sitzung läuft** — dieselbe Regel wie für Modulauswahl und Tätigkeitsfeld. Das Modul der laufenden Sitzung steht bereits in der Datei und soll nicht unter ihr weggezogen werden können.

## 2. Play und Stop

**Play** ist nur bedienbar, wenn ein Modul gewählt ist. Beim Druck schreibt die App sofort einen offenen Eintrag in `zeiten.json` — mit neuer Kennung, Startzeitpunkt, Modulname und Freitext, aber ohne Ende. Ab dann läuft die Zeitanzeige.

**Stop** ergänzt den offenen Eintrag um den Endzeitpunkt und die berechnete Dauer und schließt ihn ab. Danach ist wieder der Grundzustand „kein Eintrag läuft" erreicht.

Solange eine Sitzung läuft, sind **Modulauswahl und Textfeld gesperrt**. Beides gehört zu dem Eintrag, der bereits auf der Platte steht; nachträglich daran zu drehen, ohne dass es dort ankommt, wäre irreführend.

## 2a. Schließen bei laufender Sitzung

Wird das Fenster geschlossen, während eine Sitzung läuft, **fragt die App nach** und bietet drei Antworten:

- **Sitzung stoppen und beenden** — der Normalfall.
- **Offen lassen und beenden** — der Eintrag bleibt ohne Ende stehen. Gedacht für den Wechsel zum anderen Rechner, wo die Sitzung fortgesetzt werden soll.
- **Doch nicht beenden** — das Fenster bleibt.

Ohne diese Nachfrage müsste man sich für eines von beiden entscheiden, und beide Fälle kommen vor.

## 2b. Die kompakte Ansicht

Ein Knopf **Kompakt** neben *Auswertung…* zieht das Fenster auf das Nötigste zusammen: die laufende Zeitanzeige, **Play**, **Stop** und ein drittes Symbol, das in die große Ansicht zurückführt. Modulauswahl, Tätigkeitsfeld, der Auswertungs-Knopf und der Meldebereich sind dann ausgeblendet. Play und Stop ändern sich dabei **nicht** — sie tragen in beiden Ansichten dasselbe Symbol.

**Auch die beiden Werkzeuge in der Titelleiste verschwinden** — Modulpflege und Wahl der Darstellung. Sie nützen hier nichts: Die Modul-Liste zu pflegen ergibt ohne sichtbare Auswahl keinen Sinn, und die Darstellung stellt niemand nebenbei um. Zugleich braucht die schmale Titelleiste den Platz für den Modulnamen und das Kreuz zum Schließen.

**Die Zeitanzeige ist kleiner als in der großen Ansicht.** In voller Größe stieße sie im niedrigen Fenster oben und unten an.

**In dieser Ansicht liegt das Fenster über allen anderen.** Beides gehört zusammen: Ein winziges Fenster, das hinter dem Browser verschwindet, nützt nichts. Getrennt schalten lässt es sich deshalb nicht.

**Der Knopf ist gesperrt, solange kein Modul gewählt ist** — dieselbe Bedingung wie bei Play. So hat Play in der kompakten Ansicht immer ein Modul, obwohl die Auswahl dort nicht zu sehen ist. Anders als Play bleibt der Knopf aber bedienbar, **während eine Sitzung läuft**; genau dafür ist die Ansicht gedacht.

**Das laufende Modul steht in der Titelleiste**, zusätzlich als Tooltip auf der Zeitanzeige. Der Tooltip ist dabei kein Schmuck, sondern der verlässliche Weg: In der Titelleiste steht der Name **allein und bei Bedarf gekürzt**, ohne „Zeiterfassung —" davor. Die selbstgezeichnete Titelleiste kürzt einen zu langen Titel nämlich nicht von sich aus, sondern schiebt die Fensterknöpfe nach rechts hinaus — bei einem 300 Pixel breiten Fenster war das Kreuz zum Schließen dadurch nicht mehr erreichbar. Am Fenster selbst bleibt der vollständige Titel stehen, wo Windows ihn für Taskleiste und Fensterwechsel liest.

**Minimieren gibt es in dieser Ansicht nicht.** Das passt zur Absicht — ein Fenster, das ausdrücklich über allen anderen liegt, soll nicht verschwinden — und schafft zugleich den Platz, den die schmale Titelleiste für das Kreuz zum Schließen braucht. Zurück in der großen Ansicht ist das Minimieren wieder da.

**Eine Meldung klappt die große Ansicht wieder auf.** Der Meldebereich ist kompakt nicht zu sehen, und Meldungen gehören nach Abschnitt 5 dorthin und nicht in ein Meldungsfenster. Statt einen dritten Weg zu erfinden, kehrt die App zurück und zeigt die Meldung an ihrem Platz.

**Gemerkt wird der Modus nicht.** Die App startet immer groß. Das zu ändern hieße, `einstellungen.json` um ein Feld zu erweitern — und damit auf Formatversion 2 zu springen, was die vorhandene Datei auf jedem Rechner ungültig machte.

## 3. Erster Start auf einem Rechner

Findet die App keine lokale Einstellungsdatei, öffnet sie einen **Ordner-Dialog**. Der Benutzer wählt den Ordner, in dem die Datendateien liegen (typischerweise im OneDrive-Ordner). Die App merkt sich den Pfad in `AppData` und findet die Dateien bei jedem weiteren Start dort wieder. Fehlende Datendateien legt sie dabei an — **vorhandene rührt sie nicht an**, denn auf dem zweiten Rechner liegen im gewählten Ordner bereits die synchronisierten Daten des ersten.

**Bricht der Benutzer den Dialog ab**, beendet sich die App nicht und fragt auch nicht erneut. Das Fenster bleibt stehen, meldet, dass noch kein Ordner gewählt ist, und hält einen Knopf **Ordner wählen…** bereit; Play ist gesperrt. Das ist derselbe Weg wie bei einem später verschwundenen Ordner — eine Mechanik statt zweier.

## 3a. Ein zweiter Start, während die App schon läuft

Die App lässt sich je Windows-Sitzung **nur einmal öffnen**. Wird sie erneut gestartet, während sie schon läuft, entsteht kein zweites Fenster: Der zweite Start holt das vorhandene Fenster nach vorn — stellt es dabei auch wieder her, falls es minimiert war — und beendet sich dann **wortlos**.

Eine Meldung erscheint bewusst nicht. Der Benutzer sieht das Fenster, das er sucht, unmittelbar vor sich; ein zusätzliches Hinweisfenster wäre nur ein Klick mehr. Die Regel aus Abschnitt 5 — alle Meldungen in den Meldebereich — bleibt damit unangetastet: Es entsteht gar keine Meldung, die irgendwohin müsste.

Der Grund ist kein ästhetischer. Zwei laufende Fenster arbeiten auf zwei unabhängigen Ständen derselben `zeiten.json`, und das zweite wirft beim nächsten Schreiben die Einträge des ersten weg.

Misslingt das Nach-vorn-Holen — Windows kann es verweigern und stattdessen die Schaltfläche in der Taskleiste blinken lassen, und ganz zu Beginn des Starts hat die erste Instanz womöglich noch kein Fenster —, beendet sich die zweite Instanz trotzdem. Die Sperre gilt in jedem Fall; nur die Höflichkeit misslingt dann.

**Nicht verhindert wird der Start auf beiden Rechnern gleichzeitig.** Die Sperre ist lokal. PC und Notebook gleichzeitig zu betreiben, bleibt eine Betriebsregel, die der Benutzer selbst einhält (siehe `ARCHITECTURE.md`, Abschnitt 6).

## 4. Start mit einem offenen Eintrag

Findet die App beim Start in `zeiten.json` einen Eintrag ohne Ende — nach einem Absturz oder weil ohne Stop zum anderen Rechner gewechselt wurde —, zeigt sie einen **Dialog mit beiden Wegen**:

- **Abschließen** — der Benutzer gibt die Endzeit ein, der Eintrag wird fertig.
- **Weiterlaufen** — die App zählt ab dem gespeicherten Startzeitpunkt weiter, als wäre nie gestoppt worden; der Zustand geht in „ein Eintrag läuft" über.

Beide Wege werden **immer** angeboten; der Benutzer entscheidet je nach Situation. Die App unterscheidet nicht selbst, ob der offene Eintrag von vorhin oder von gestern stammt — deshalb liegt die Wahl bewusst beim Benutzer.

Der Dialog ist ein **eigenes kleines Fenster**, kein Standard-Meldungsfenster: Die Endzeit muss eingegeben werden können. Er zeigt Modul, Beginn, gegebenenfalls die Tätigkeit und die seither vergangene Zeit — Letzteres, damit sofort auffällt, wenn der Eintrag von gestern stammt. Das Eingabefeld ist mit dem aktuellen Zeitpunkt vorbelegt und wird bei jeder Änderung geprüft; **Abschließen** bleibt gesperrt, solange die Eingabe kein Zeitpunkt nach dem Beginn ist. Eingegeben wird ohne Zeitzonenangabe, es gilt die Ortszeit dieses Rechners.

Schließt der Benutzer den Dialog über das Fensterkreuz, ohne sich zu entscheiden, **bleibt der Eintrag offen**. Die Frage stellt sich dann beim nächsten Start erneut.

## 5. Meldungen und Fehler

Der Meldebereich zeigt an, was gerade passiert oder schiefgegangen ist:

- **Daten geladen** mit dem Ordner, aus dem gelesen wurde.
- **Datei nicht gefunden** — der gemerkte Ordner enthält die erwartete Datei nicht (verschoben, umbenannt, noch nicht synchronisiert). Die App bietet an, den Ordner neu zu wählen.
- **Falsche Formatversion** — die Datei wurde abgelehnt, weil ihre Version nicht passt. Sie wird nicht gedeutet.
- **Noch kein Modul eingetragen** — die Modul-Liste ist leer. Beim ersten Start ist das der Normalfall und kein Fehler: Die App hat `module.json` gerade erst als leere Vorlage angelegt. Die Meldung sagt deshalb nicht „etwas ist kaputt", sondern weist auf das Bücherregal-Symbol in der Titelleiste hin und nennt zusätzlich den vollständigen Pfad der Datei, für den, der sie lieber selbst bearbeitet.
- **Kein Modul steht zur Wahl** — es gibt Module, aber alle sind deaktiviert. Auch hier führt der Weg über das Bücherregal-Symbol in der Titelleiste.

  Bei diesen beiden Meldungen erscheint der Knopf **Ordner wählen…** ausdrücklich **nicht**: Der Datenordner ist in Ordnung, ihn neu zu wählen wäre der falsche Rat.

**Alle Meldungen erscheinen im Meldebereich**, nicht als wegzuklickende Fenster. Die einzige Ausnahme ist die Nachfrage beim Schließen mit laufender Sitzung (Abschnitt 2a): Dort ist eine Antwort nötig, bevor es weitergeht.

### Wie der Meldebereich aussieht

Er ist ein **abgesetzter Kasten** mit Rahmen im unteren Teil des Fensters, nicht bloß eine Zeile Text. Links steht ein Symbol, rechts daneben eine **fette Überschrift**, die in drei bis vier Worten sagt, was los ist, und darunter der erklärende Text.

- **Statusmeldungen** — ruhiger Ton, Informationssymbol. „Daten geladen", „Läuft seit …", „Gespeichert: …".
- **Meldungen, die eine Handlung verlangen** — Warnfarbe und Warnsymbol, und der Knopf **Ordner wählen…** erscheint im Kasten. Play und Stop bleiben gesperrt, solange nichts geladen ist.

Beide Töne kommen aus der Farbtafel von Windows und sehen deshalb im hellen wie im dunklen Modus richtig aus (Abschnitt 0).

Enthält eine Meldung einen Dateipfad, steht er auf einer **eigenen Zeile**, damit er lesbar und kopierbar ist. Das Fenster ist hoch genug, dass auch eine mehrzeilige Meldung vollständig hineinpasst — ein zu knapper Meldebereich schneidet sonst genau den Teil ab, der sagt, was zu tun ist.

Fehler führen nie dazu, dass die App stillschweigend etwas Falsches tut. Im Zweifel meldet sie und wartet auf eine Aktion des Benutzers.

## 6. Das Auswertungsfenster

Ein Knopf **Auswertung…** im Hauptfenster öffnet ein zweites Fenster. Es ist **nicht modal** — das Erfassen läuft weiter, während man hineinsieht — und es wird höchstens eines geöffnet: Ein zweiter Klick holt das vorhandene nach vorn. Der Knopf ist gesperrt, solange kein Datenordner geladen ist.

Das Fenster liest `zeiten.json` bei jedem Aufbau **frisch ein**. Die Datei ist die Wahrheit, nicht ein Stand im Speicher — sie wandert über OneDrive und kann von Hand repariert worden sein.

### Der Zeitraum

Oben stehen zwei Datumsfelder **Von** und **Bis**, beide dürfen leer bleiben. Leer heißt „ohne Grenze"; sind beide leer, wird alles ausgewertet. Die gewählten Tage zählen **einschließlich** mit. Ein Knopf **Alles** leert beide Felder, ein Knopf **Aktualisieren** liest die Datei erneut.

Liegt der Bis-Tag vor dem Von-Tag, erscheint eine Meldung am unteren Rand und die Tabellen bleiben stehen, wie sie waren.

### Die Module

Darunter eine zweite Leiste mit zwei Bedienelementen, die für **Wochen, Tage und Einträge** gelten:

- **Modulfilter.** Ein Knopf, der auf *alle* steht, solange nichts gewählt ist, klappt eine Liste mit einem Häkchen je Modul auf. Angeboten wird jedes Modul, auf dem je Zeit gebucht wurde — nicht nur die des gewählten Zeitraums, damit ein Modul nicht aus der Liste verschwindet, nur weil der Zeitraum es nicht berührt. Mehrere Häkchen sind möglich; *Alle Module* in der Liste hebt die Auswahl auf. Ohne Häkchen zählen alle Module: Eine leere Auswahl heißt „keine Beschränkung", nicht „nichts". Steht ein Häkchen, nennt der Knopf das Modul, bei mehreren „2 von 5".
- **Je Modul / Nur Summe.** Standard ist *Je Modul*. Der Reiter *Module* richtet sich weder nach dem Filter noch nach dem Umschalter: Er zeigt ohnehin alle Module nebeneinander.

Der Filter wirkt auch auf die **Gesamtzeile** oben (sie nennt dann die gewählten Module) und auf die **CSV-Datei** — ausgegeben wird, was das Fenster zeigt.

### Was zu sehen ist

Über den Tabellen steht die Gesamtzeit des Zeitraums samt Anzahl der Einträge. Läuft gerade eine Sitzung, folgt darunter der Hinweis, dass sie **in keiner Summe enthalten** ist — sie hat ja noch keine Dauer.

Darunter vier Reiter:

- **Module** — je Modul die Gesamtzeit und die Anzahl der Einträge, das Meiste oben. Steht ein Modul nicht mehr in `module.json`, erscheint in der Spalte **Hinweis** ein entsprechender Vermerk; die Zeit zählt trotzdem voll mit.
- **Wochen** — je Kalenderwoche nach ISO 8601 (Montag bis Sonntag) die Gesamtzeit, chronologisch. Wochen ohne Arbeit dazwischen erscheinen mit 00:00:00. Bei *Je Modul* steht zwischen Woche und Summe eine **Zeitspalte je Modul**, und zwar für jedes Modul, das im gewählten Zeitraum (und Filter) Zeit hat.
- **Tage** — dasselbe je Kalendertag.
- **Einträge** — die einzelnen Einträge, der jüngste zuerst, mit Tag, Uhrzeit von–bis, Modul, Tätigkeit und Dauer. Hier erscheint auch der offene Eintrag; seine Dauer steht als „läuft".

Ein Eintrag gehört zu dem Tag und der Woche, an dem er **beginnt** — eine Sitzung über Mitternacht wird nicht geteilt.

### Die Diagramme

In den Reitern *Module*, *Wochen* und *Tage* steht über der Tabelle ein **Diagramm**: liegende Balken bei den Modulen, stehende Säulen bei Wochen und Tagen. Der Reiter *Einträge* hat keines — eine Liste von Tätigkeiten ist kein Diagramm.

Beides zugleich zu sehen ist der Sinn: Das Diagramm zeigt das **Verhältnis**, die Tabelle die **genauen Zahlen**. Beide stammen aus derselben Rechnung und können deshalb nicht auseinanderlaufen.

Die Achse rechnet in **Stunden als Dezimalzahl** — eine Achse voller 01:35:00 wäre nicht lesbar. Wer den genauen Wert will, fährt mit dem Zeiger über einen Balken: Im Tooltip steht die gewohnte Form Stunden:Minuten:Sekunden.

Die Diagramme werden von einer eigenen Bibliothek gezeichnet, die die Farben von Windows nicht von sich aus kennt. Achsen, Beschriftungen, Balken und Tooltips bekommen sie deshalb ausdrücklich zugewiesen, und das Fenster **baut sich neu auf**, wenn Windows im laufenden Betrieb zwischen hell und dunkel wechselt. Ohne das stünde die Beschriftung nach einem Wechsel schwarz auf schwarzem Grund.

**Wochen und Tage bei *Je Modul*:** Je Zeitraum steht **eine Säule pro Modul nebeneinander** und rechts davon eine **Summensäule**. Nebeneinander statt gestapelt, weil ein gestapelter Balken die Summe schon zeigte und die Einzelwerte schwerer ablesbar wären. Die Summe ist neutral grau, damit sie nicht für ein Modul gehalten wird; die Module haben feste Farben aus einer Palette für Farbfehlsichtige und behalten ihre Farbe, während andere Module an- und abgehakt werden. Unter dem Diagramm steht eine Legende. **Hat im gewählten Zeitraum nur ein Modul Zeit** — weil im Filter nur eines angehakt ist oder nur an einem gearbeitet wurde —, **entfällt die Summensäule**: Sie wäre genauso hoch wie die Modulsäule. Die Tabelle behält dagegen Modulspalte und Zeitspalte. Bei *Nur Summe* gibt es nur die eine Säule, in der Akzentfarbe wie bisher.

**Die Durchschnittslinie** läuft in beiden Fällen **gestrichelt** quer durch Wochen- und Tagesdiagramm. Sie zeigt die mittlere Gesamtzeit je Woche beziehungsweise je Tag; leere Wochen und Tage zwischen dem ersten und dem letzten belegten zählen **mit null** mit, so wie sie auch in der Tabelle stehen. Bei aktivem Filter ist es der Durchschnitt der gefilterten Summe. Den genauen Wert nennt der Tooltip.

**Der Tooltip im Diagramm** gehört immer nur zu der Säule oder Linie unter dem Zeiger und sitzt **oberhalb** davon; er zeigt nicht alle Module des Zeitraums auf einmal. Damit er auch über der höchsten Säule Platz hat, reicht die Stundenachse um die Hälfte über den größten Wert hinaus; ohne diesen Streifen schöbe die Bibliothek den Tooltip nach unten auf genau die Säule, die er beschreibt. Sein Hintergrund ist deckend, damit Säulen und Durchschnittslinie nicht durchscheinen. Beim Zoomen mit dem Mausrad wird er ausgeblendet. Der Hinweis zu Zoomen und Schieben steht als kleine graue Textzeile rechts über dem Diagramm, nicht als Tooltip — ein zweiter Tooltip überlagerte sonst den des Diagramms. Ein Tooltip am Reiter genügt dafür nicht, denn WPF zeigt ihn auch über dem Inhalt des Reiters an.

Bei *Wochen* und *Tagen* lässt sich mit dem Mausrad **zoomen** und mit gedrückter Maustaste **schieben**. Über einige Monate hinweg werden es mehr Säulen, als nebeneinander lesbar sind. Bei den Modulen ist das nicht nötig und deshalb auch nicht eingeschaltet.

### Ausgeben für Excel

Ein Knopf **Nach CSV…** in der oberen Leiste schreibt die Einträge des gewählten Zeitraums als CSV-Datei. Ausgegeben wird, was das Fenster zeigt — derselbe Zeitraum, dieselben Einträge; nicht die Summen, sondern die einzelnen Sitzungen, aus denen sich in Excel jede Summe selbst bilden lässt.

Windows fragt nach Ort und Namen und schlägt `zeiterfassung-JJJJ-MM-TT.csv` vor. Danach steht unten im Fenster in ruhigem Grau, wie viele Einträge geschrieben wurden und wohin — und ob ein laufender Eintrag ausgelassen wurde, denn der hat noch keine Dauer.

Aufbau und Kodierung der Datei stehen in `DATENFORMAT.md`, Abschnitt 6.

### Was es nicht kann

Die Tabellen sind **nur zum Ansehen**. Ein Eintrag lässt sich hier weder ändern noch löschen; dafür ist nach wie vor `zeiten.json` da. Auch der Weg über die CSV-Datei führt nur hinaus: Die App liest eine solche Datei nie wieder ein.
