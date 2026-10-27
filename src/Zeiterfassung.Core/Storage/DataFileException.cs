namespace Zeiterfassung.Core.Storage;

/// <summary>
/// Reports that a data file could not be read or could not be written.
/// </summary>
/// <remarks>
/// The message is already German and ready to display: it names the file concerned and, where
/// that helps, the entry concerned. The shell catches this exception and writes its message
/// into the message area without deciding anything itself — the judgement thereby stays in the
/// core, where it can be tested.
/// </remarks>
public sealed class DataFileException : Exception
{
	/// <summary>
	/// Creates the exception with a displayable message.
	/// </summary>
	/// <param name="message">The plain German message for the user.</param>
	public DataFileException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Creates the exception with a displayable message and the underlying cause.
	/// </summary>
	/// <param name="message">The plain German message for the user.</param>
	/// <param name="innerException">The technical exception behind it.</param>
	public DataFileException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
