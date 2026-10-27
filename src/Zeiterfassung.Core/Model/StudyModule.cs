namespace Zeiterfassung.Core.Model;

/// <summary>
/// A study module, as it stands in the module file.
/// </summary>
/// <remarks>
/// Named <c>StudyModule</c> and not <c>Module</c>, because the latter clashes by name with
/// <see cref="System.Reflection.Module"/> — a type that stands for something entirely
/// different in .NET.
/// </remarks>
public sealed class StudyModule
{
	/// <summary>
	/// Creates a study module with the given name.
	/// </summary>
	/// <param name="name">The module name; surrounding whitespace is removed.</param>
	/// <param name="isActive">Whether the module is offered for tracking; without one, yes.</param>
	/// <exception cref="ArgumentException">When the name is empty or only whitespace.</exception>
	public StudyModule(string name, bool isActive = true)
	{
		Name = RequireName(name);
		IsActive = isActive;
	}

	/// <summary>
	/// The module name, as it appears in the selection and is written into the time entry.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Whether the module is still offered for tracking.
	/// </summary>
	/// <remarks>
	/// A finished module is <em>deactivated</em> rather than deleted: its name stands in plain
	/// text in every time entry that ran on it. It thereby disappears from the
	/// selection but stays in the file, and its time keeps counting in the report unchanged.
	/// </remarks>
	public bool IsActive { get; }

	/// <summary>
	/// Checks a module name and returns it without surrounding whitespace.
	/// </summary>
	/// <remarks>
	/// Removing the whitespace is the only silent change to the contents of the hand-maintained
	/// file. It is harmless and keeps a stray trailing space from creating a second, seemingly
	/// identical module name.
	/// </remarks>
	/// <param name="name">The module name to check.</param>
	/// <returns>The checked module name without surrounding whitespace.</returns>
	/// <exception cref="ArgumentException">When the name is empty or only whitespace.</exception>
	private static string RequireName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new ArgumentException("Ein Modul braucht einen Namen.", nameof(name));
		}

		return name.Trim();
	}
}
