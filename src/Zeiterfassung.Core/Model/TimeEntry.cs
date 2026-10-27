namespace Zeiterfassung.Core.Model;

/// <summary>
/// A single time entry — one work session on a module.
/// </summary>
/// <remarks>
/// An entry without an end <em>is</em> the open entry; there is no separate status field.
/// From the outside only the end is mutable, and that solely through
/// <see cref="CompleteEntry"/>. A completed entry can thereby not be closed a second time or
/// backwards by accident.
/// </remarks>
public sealed class TimeEntry
{
	/// <summary>
	/// Creates an entry from values that have already been checked.
	/// </summary>
	/// <remarks>
	/// Private, so that only the two named ways stand open from the outside: a new entry
	/// through <see cref="StartEntry"/>, or one read from the file through
	/// <see cref="RestoreEntry"/>.
	/// </remarks>
	/// <param name="id">The identifier of the entry.</param>
	/// <param name="start">The start of the session.</param>
	/// <param name="end">The end, or null for an open entry.</param>
	/// <param name="module">The module name in plain text.</param>
	/// <param name="note">The activity as free text.</param>
	private TimeEntry(Guid id, DateTimeOffset start, DateTimeOffset? end, string module, string note)
	{
		Id = id;
		Start = TruncateToSeconds(start);
		End = end is null ? null : TruncateToSeconds(end.Value);
		Module = module;
		Note = note;
	}

	/// <summary>
	/// The identifier of the entry.
	/// </summary>
	public Guid Id { get; }

	/// <summary>
	/// The start of the session.
	/// </summary>
	public DateTimeOffset Start { get; }

	/// <summary>
	/// The end of the session, or null while it is still running.
	/// </summary>
	/// <remarks>
	/// The absence of this value is the only sign that the entry is open — which is why it has
	/// no setter and is set by <see cref="CompleteEntry"/> alone.
	/// </remarks>
	public DateTimeOffset? End { get; private set; }

	/// <summary>
	/// The module name in plain text.
	/// </summary>
	/// <remarks>
	/// Deliberately the name and not a reference into the module file: an entry thereby stays
	/// readable even when <c>module.json</c> is missing or the module was struck from it.
	/// </remarks>
	public string Module { get; }

	/// <summary>
	/// The activity as free text, possibly empty.
	/// </summary>
	public string Note { get; }

	/// <summary>
	/// States whether the entry is still running.
	/// </summary>
	public bool IsOpen => End is null;

	/// <summary>
	/// The time elapsed between start and end, or null for an open entry.
	/// </summary>
	/// <remarks>
	/// The duration is computed afresh on every access and held nowhere. That makes the rule
	/// „start and end are authoritative“ from DATENFORMAT.md, section 2 not merely something to
	/// observe but something impossible to circumvent: a duration that does not match the
	/// timestamps cannot arise in the running program at all. When writing it is additionally
	/// put into the file; when reading it is discarded.
	/// </remarks>
	public TimeSpan? Duration => End is null ? null : End.Value - Start;

	/// <summary>
	/// Begins a new session and creates an open entry for it.
	/// </summary>
	/// <remarks>
	/// The identifier is assigned here and not only when writing — the entry is meant to be
	/// complete before it reaches the disk.
	/// </remarks>
	/// <param name="start">The start of the session.</param>
	/// <param name="module">The module name in plain text.</param>
	/// <param name="note">The activity as free text; null is treated as empty.</param>
	/// <returns>An open entry with a new identifier and without an end.</returns>
	/// <exception cref="ArgumentException">When the module name is empty or only whitespace.</exception>
	public static TimeEntry StartEntry(DateTimeOffset start, string module, string? note)
	{
		return new TimeEntry(Guid.NewGuid(), start, null, RequireModule(module), note ?? string.Empty);
	}

	/// <summary>
	/// Restores an entry the way it stands in the file.
	/// </summary>
	/// <remarks>
	/// The way in for everything that comes from <c>zeiten.json</c>. The checks here are the
	/// place where an entry damaged by hand comes to light; the calling storage layer turns the
	/// exception into a rejection of the whole file.
	/// </remarks>
	/// <param name="id">The stored identifier.</param>
	/// <param name="start">The stored start.</param>
	/// <param name="end">The stored end, or null for an open entry.</param>
	/// <param name="module">The stored module name.</param>
	/// <param name="note">The stored free text; null is treated as empty.</param>
	/// <returns>The restored entry.</returns>
	/// <exception cref="ArgumentException">When the identifier is empty or the module name is missing.</exception>
	/// <exception cref="ArgumentOutOfRangeException">When the end lies before the start.</exception>
	public static TimeEntry RestoreEntry(Guid id, DateTimeOffset start, DateTimeOffset? end, string module, string? note)
	{
		if (id == Guid.Empty)
		{
			throw new ArgumentException("Der Eintrag hat keine Kennung.", nameof(id));
		}

		if (end is not null)
		{
			RequireEndAfterStart(start, end.Value, nameof(end));
		}

		return new TimeEntry(id, start, end, RequireModule(module), note ?? string.Empty);
	}

	/// <summary>
	/// Closes the running entry with the given end.
	/// </summary>
	/// <param name="end">The end of the session.</param>
	/// <exception cref="InvalidOperationException">When the entry is already completed.</exception>
	/// <exception cref="ArgumentOutOfRangeException">When the end lies before the start.</exception>
	public void CompleteEntry(DateTimeOffset end)
	{
		if (!IsOpen)
		{
			throw new InvalidOperationException("Der Eintrag ist bereits abgeschlossen.");
		}

		DateTimeOffset truncatedEnd = TruncateToSeconds(end);
		RequireEndAfterStart(Start, truncatedEnd, nameof(end));
		End = truncatedEnd;
	}

	/// <summary>
	/// Cuts the fractions of a second off a timestamp.
	/// </summary>
	/// <remarks>
	/// DATENFORMAT.md fixes second precision, but the system clock delivers seven decimal
	/// places. The truncation happens here and not only when writing, so that the running
	/// display never shows a different duration than the file.
	/// </remarks>
	/// <param name="value">The timestamp to truncate.</param>
	/// <returns>The same timestamp, cut to whole seconds.</returns>
	private static DateTimeOffset TruncateToSeconds(DateTimeOffset value)
	{
		return new DateTimeOffset(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), value.Offset);
	}

	/// <summary>
	/// Checks that a module name is present and returns it unchanged.
	/// </summary>
	/// <param name="module">The module name to check.</param>
	/// <returns>The checked module name.</returns>
	/// <exception cref="ArgumentException">When the module name is empty or only whitespace.</exception>
	private static string RequireModule(string module)
	{
		if (string.IsNullOrWhiteSpace(module))
		{
			throw new ArgumentException("Ein Zeiteintrag braucht einen Modulnamen.", nameof(module));
		}

		return module;
	}

	/// <summary>
	/// Checks that an end does not lie before the start.
	/// </summary>
	/// <remarks>
	/// Equality is allowed: an entry stopped again at once, with a duration of zero, is
	/// nonsensical but not contradictory — and keeping the user from it is not the model's job.
	/// </remarks>
	/// <param name="start">The start.</param>
	/// <param name="end">The end to check.</param>
	/// <param name="parameterName">The parameter name named in the exception.</param>
	/// <exception cref="ArgumentOutOfRangeException">When the end lies before the start.</exception>
	private static void RequireEndAfterStart(DateTimeOffset start, DateTimeOffset end, string parameterName)
	{
		if (end < start)
		{
			throw new ArgumentOutOfRangeException(
				parameterName,
				end,
				$"Der Endzeitpunkt liegt vor dem Startzeitpunkt {start:o}.");
		}
	}
}
