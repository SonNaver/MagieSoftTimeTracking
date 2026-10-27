namespace Zeiterfassung.Core.Tests.Workflow;

/// <summary>
/// A clock that stands still until the test moves it on.
/// </summary>
/// <remarks>
/// Written by hand and not taken from a package: Microsoft's fake clock sits in a NuGet
/// package of its own, and the exception to the package rule covers the test framework alone.
/// <see cref="TimeProvider"/> itself belongs to the standard library and can be derived from
/// in a few lines.
/// </remarks>
/// <param name="now">The point in time the clock stands at to begin with.</param>
internal sealed class StoppedClock(DateTimeOffset now) : TimeProvider
{
	/// <summary>
	/// The point in time the clock currently shows.
	/// </summary>
	public DateTimeOffset Now { get; set; } = now;

	/// <summary>
	/// Moves the clock forward by the given span.
	/// </summary>
	/// <param name="amount">The span to move forward by.</param>
	public void AdvanceClock(TimeSpan amount)
	{
		Now += amount;
	}

	/// <summary>
	/// Returns the point in time the clock is set to.
	/// </summary>
	/// <returns>The point in time the clock stands at.</returns>
	public override DateTimeOffset GetUtcNow()
	{
		return Now;
	}
}
