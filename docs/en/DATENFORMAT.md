# Data format — MagieSoft TimeTracking

> **English version of `docs/DATENFORMAT.md`, reflecting the German original as of 27 October
> 2026.** The German version is authoritative; where the two differ, it applies. Document names
> below refer to the German originals in `docs/`; four of them also exist in English in this
> folder.

> To be read before any access to the data. Describes the three data files and the rules by which
> they are read and written — and, in section 6, the output file for Excel, which the app writes
> but never reads again.
>
> File names and key names are settled: German file names, because the user sees them in Explorer
> and repairs them by hand — English key names, because they follow the rule "code in English" and
> are transparent to German eyes as well.

## 1. The three files at a glance

| File | Content | Location | Travels? |
|---|---|---|---|
| `zeiten.json` ("times") | the time entries | chosen folder | yes |
| `module.json` ("modules") | the module list, with a flag per module | chosen folder | yes |
| `einstellungen.json` ("settings") | folder path and appearance | locally in `AppData\Local` | no |

## 2. `zeiten.json` — the time file

The file is **a single object** with two things: a **format version** under the key
`formatVersion` and an **array of entries**. The version is at the top so that the app can check
whether the file fits it before interpreting anything.

The format version is checked on reading. If it does not match or is missing, the whole file is
rejected and reported — it is never interpreted "as well as possible".

### The format version

`formatVersion` is an integer and currently stands at **1**. It is checked for **exact equality**:
any other value leads to rejection, even a smaller one. There is deliberately no migration of older
states — that would be effort for a case that does not exist yet. Should a version 2 ever arise,
the changeover will be decided then.

### Fields of an entry

| Key | Type | Required | Meaning |
|---|---|---|---|
| `id` | text (GUID) | yes | Unique identifier of the entry. A long, random, practically globally unique string. |
| `start` | timestamp | yes | Start time, date and time of day together. Form see "The time format" below. |
| `end` | timestamp | no | End time. **If this field is missing (or empty), the entry is open** — it is still running or was never ended properly. |
| `module` | text | yes | The module name **in plain text**, not as a reference into the configuration. |
| `note` | text | no | The activity as free text. May be empty. |
| `duration` | duration | no | The stored duration as text in the pattern hours:minutes:seconds. Empty for an open entry. |

### What the file looks like

A time file with two entries — one complete and one open. This is exactly how the app writes it,
and exactly how it has to look again after a repair by hand:

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

The second entry shows the essential point: **for the open entry, `end` and `duration` are missing
entirely** — they do not appear as empty fields. Whoever completes it by hand adds both; whoever
enters only `end` and leaves out `duration` is also right, because the app recomputes the duration
anyway.

### The time format

Both timestamps follow **ISO 8601 as local time with an appended time zone offset**: first the
date, then the time of day, then the deviation from universal time — in our time zone two hours in
summer, one in winter. The time the user would have read off their wristwatch thus comes first and
is readable at once, while the offset makes it unambiguous for the machine. The decisive case is
the night of the autumn clock change, in which the same time of day occurs twice: without an
offset, a session across that point would yield a duration that is wrong by an hour.

**Precision:** to the second. The duration is likewise kept in seconds and shown in the interface
as hours:minutes:seconds.

**Missing offset after a repair by hand:** if the user enters only date and time of day during a
repair and leaves out the offset, the timestamp is interpreted as **local time of this computer**
— the way it was meant. On the next write the app silently adds the offset. Repairing thereby
stays simple: typing date and time of day is enough. This is the one deliberate exception to the
otherwise strict rule "reject when in doubt"; it concerns no ambiguous format, only a missing
addition that the app can reliably supply itself.

### Impossible entries

After a repair by hand an entry can contradict itself without the file being broken: an end
**before** the start, a missing module name, a missing identifier. The app then rejects the
**whole file** and reports it — the same strictness as for a wrong format version.

The message names the **identifier of the affected entry**, so that the user can find it in the
text editor. Nothing is lost in the process: the file stays untouched until it is repaired.

The obvious gentler way — discard only the broken entry and carry on with the rest — has been
deliberately rejected. The app would rewrite the file completely at the next Stop, and the
discarded entry would be gone for good without the user ever having seen it.

An end that lies **exactly on** the start, however, is allowed. The duration is then zero —
pointless, but not contradictory.

### The open entry

There is no field of its own for "is open". An entry without an end time **is** the open entry.
That way there can be at most one, and the state can be read off in exactly one place.

### The duration rule

The duration is stored in addition, but **start and end are always authoritative.**

- On writing, the app computes the duration afresh from `start` and `end` and stores it alongside.
  The app therefore never produces a contradiction.
- A contradiction can only arise through a repair by hand — when someone changes `start` or `end`
  and leaves `duration` as it was.
- If `duration` and (`end` − `start`) do not agree on reading, **the app silently overwrites the
  stored duration** with the computed value. The repair to the times thus prevails automatically.

## 3. `module.json` — the module configuration

Also **an object** with a **format version** under the key `formatVersion` and an **array of
modules**. Since 26 September 2026 the version stands at **2** and is checked for exact equality.

**It is maintained in two ways that exist side by side.** The app creates modules
itself on request and rewrites the whole file in doing so; in addition, the user may still edit it
by hand. If the app finds no `module.json` in the chosen folder, it creates an empty template —
with the correct format version and an empty module array. As long as the list is empty, the app
reports "Noch kein Modul eingetragen" ("no module entered yet", see `OBERFLAECHE.md`, section 5).

Each module is **an object with a name field** — not a bare text. That leaves room for later
additional details such as an abbreviation or a sort order without changing the shape of the file.

### On format version 2

Version 1 knew only the name per module. With `isActive` a second field was added, and every
extension of the format costs a jump of the version.

**Unlike with `einstellungen.json`, this jump affects both computers.** The module configuration
travels via OneDrive: as soon as one computer has written it in version 2, the older app build on
the other computer rejects it. The order for the changeover is therefore: **first update both
computers, then maintain modules.** An existing file of version 1 is raised to 2 by hand — changing
the number is enough, because the new field may be missing.

**Unknown fields are not tolerated.** If someone enters a field the app does not know, the file is
rejected — the same strictness as everywhere else. Extensibility is unaffected by this, but it
costs a jump of the format version: a new field comes with a new version, not on the side.

### What the file looks like

The app creates the file empty on first start — only with format version and an empty list.
Filled, it looks like this:

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

In the example, "Betriebssysteme" is deactivated and no longer offered for recording; its time
still counts in the report unchanged. For "Statistik" the field is missing — that is allowed and
means the same as `true`.

The more compact notation with each module on one line is equally valid — JSON does not care
about indentation. All that matters is that each module is **an object with `name`** and not just
a text in quotation marks.

### Rules for the module list

- **No name may occur twice.** The comparison ignores upper and lower case. The reason: the module
  name stands in plain text in every time entry and is the only link there — two modules with the
  same name could no longer be told apart in the recorded data. On a duplicate the file is rejected
  and the affected name is reported.
- **Surrounding white space is removed.** An accidental space at the end of a line would otherwise
  create a second, seemingly identical module name. This is the only silent change the app makes to
  the content of this file.
- **The order stays as written.** The app does not sort; the user maintains the file by hand and
  finds the modules in the selection the way they wrote them down.
- **An empty list is not an error.** On first start it is the normal case (see above).

| Key | Type | Required | Meaning |
|---|---|---|---|
| `name` | text | yes | The module name as it appears in the selection and is written into the time entry. |
| `isActive` | boolean | no | Whether the module is offered for recording. If the field is missing, `true` applies. |

### Why a module in use is only deactivated

The module name stands **in plain text** in every time entry and is the only link there. If a
module were renamed, every old entry would still hang on the old name and appear in the report
with the remark "steht nicht mehr in module.json" ("no longer in module.json"); if it were
deleted, the same. A module on which time has already been booked can therefore be neither renamed
nor deleted — only `isActive` may change to `false`. A module without any entry, on the other
hand, can be changed freely, because nothing depends on it.

By hand this rule can be bypassed — on reading, the app checks only the format, not the history.
Whoever changes a module name in use in the file cuts their old entries off from it; the report
then shows them as an unknown module, but the time is preserved.

Here too the format version is checked on reading; if it differs, the file is rejected.

## 4. `einstellungen.json` — folder path and appearance

Lies under `AppData\Local\Zeiterfassung` and deliberately does not travel. The **Local** branch is
essential here: `AppData\Roaming` travels along with the user profile in company and domain
environments and would thereby trigger exactly the error that local storage is meant to prevent: a path of one
computer on the other.

The file holds two things, both of which apply per computer: the path to the folder in which
`zeiten.json` and `module.json` lie, and the chosen appearance.

| Key | Type | Required | Meaning |
|---|---|---|---|
| `formatVersion` | integer | yes | The format version, currently 2. Checked for exact equality. |
| `dataFolder` | text (path) | yes | The folder in which the two data files lie. |
| `theme` | text | no | The appearance: `system`, `light` or `dark`. Upper and lower case do not matter. If the field is missing, `system` applies. |

```json
{
  "formatVersion": 2,
  "dataFolder": "C:\\Users\\markus\\OneDrive\\Zeiterfassung",
  "theme": "system"
}
```

### On the appearance

`system` means: the app follows the app mode of Windows and changes along when it is switched
while running. `light` and `dark` are fixed choices that are independent of Windows.

**The field may be missing, but an unknown value is rejected.** Both have a reason. A file written
by hand without this field is not an error — `system` is the default and the same state in which
the app ran before this format version. If, on the other hand, it said `dunkel` there, that would
be a typo that the user would take for an effective setting; the file is therefore rejected and
the message names the allowed values.

### On format version 2

Version 1 knew only `dataFolder`. With `theme` a second content field was added, and by the rule
that applies to all three files, every extension of the format costs a jump of the version — even when
the new field is optional.

**A file of version 1 is therefore rejected**, like any other unsuitable one. The user sees the
usual message in the message area and chooses the folder once again; afterwards the file stands at
version 2. Because it lies locally and does not travel, this affects every computer exactly once
and independently of the other. A silent upgrade was rejected — the strictness applies to all three
files alike, and an exception for this would be the beginning of the end of that rule.

### Its absence is not an error

Here the file differs from the other two. If it is not there, that is the **first start on this
computer**: the app opens the folder dialog and creates the file after the choice. There is
therefore no message and no error — the most common path through the first start is not meant to
be an error path.

If, on the other hand, the file is **present but unusable** — wrong format version, broken JSON,
missing folder path —, the same strictness applies as everywhere: it is rejected and reported.

**Whether the remembered folder still exists is not checked.** Reading returns the path as it
stands. Whether the folder is there and the files lie in it becomes apparent on accessing them —
with the messages from `OBERFLAECHE.md`, section 5, and the offer to choose the folder anew. A
check on reading would be a mere snapshot anyway.

## 5. Readability for repair by hand

All content fields are in plain text — the module name as text, the times as readable timestamps,
the free text as free text. Only the identifier is a technical string; it is not touched during a
repair, because one repairs at the readable fields. In an emergency the file can thus be completed
or corrected by hand.

## 6. The output file for Excel (CSV)

This file falls outside the frame of the three before: the app **only writes it and never reads it
again**. It therefore has no format version — there is nobody who would have to check it. It is a
snapshot and takes part in no synchronisation.

**Location and name are chosen by the user** in a save dialog. `zeiterfassung-YYYY-MM-DD.csv` is
suggested.

### Structure

A header line, then one line per completed time entry, **in ascending order of start time** — the
oldest session at the top. What is written out is the period the report window is currently
showing.

| Column | Content |
|---|---|
| `Datum` (date) | The day of the start, in the notation of the computer. |
| `Beginn` (start) | The time of day of the start, to the second. |
| `Ende` (end) | The time of day of the end, to the second. |
| `Modul` (module) | The module name in plain text. |
| `Tätigkeit` (activity) | The free text; may be empty. |
| `Dauer (Stunden)` (duration in hours) | The duration as a decimal number with up to four decimal places — for computing in Excel. |
| `Dauer` (duration) | The same duration in the pattern hours:minutes:seconds — for reading. |

**The open entry is missing.** It has no duration yet; an empty cell in a column that one sums up
would be a trap. The message after the export says that one was left out.

### Encoding and characters

- **UTF-8 with byte order mark.** That is the difference from the data files, which deliberately
  carry none: without the three bytes at the start, Excel reads the file as Windows-1252 and turns
  "Tätigkeit" into "TÃ¤tigkeit".
- **Separator and decimal mark come from the Windows settings.** On German Windows, therefore,
  semicolon and comma — exactly what Excel expects there.
- **Lines end with carriage return and line feed** (CRLF), as RFC 4180 specifies.
- **Escaping follows RFC 4180.** If a field contains the separator, a quotation mark or a line
  break, it is enclosed in quotation marks; a quotation mark in the text is doubled. In practice
  this concerns the activity, because it is free text.

### What the file looks like

Three entries on German Windows, the second activity with a semicolon in it:

```
Datum;Beginn;Ende;Modul;Tätigkeit;Dauer (Stunden);Dauer
21.09.2026;08:00:00;09:30:00;Mathematik II;Uebungsblatt 3;1,5;01:30:00
22.09.2026;14:00:00;15:00:00;Statistik;"Kapitel 1; Kapitel 2";1;01:00:00
25.09.2026;20:11:56;20:52:25;Betriebssysteme;;0,6747;00:40:29
```

The third entry shows that an empty activity simply yields an empty field — two separators side
by side.
