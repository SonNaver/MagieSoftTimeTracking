using System.Text;

namespace Zeiterfassung.Core.Storage;

/// <summary>
/// Writes files so that a crash mid-write leaves no half file behind.
/// </summary>
/// <remarks>
/// The procedure: the new content goes into a side file first, then a single
/// rename replaces the old one. The rename is indivisible — either the new file is there in
/// full or the old one is untouched. A half state cannot arise.
/// </remarks>
public static class AtomicFileWriter
{
	/// <summary>
	/// The extension of the side file that is written first.
	/// </summary>
	private const string TemporarySuffix = ".tmp";

	/// <summary>
	/// Writes text into a file in a way a crash cannot damage.
	/// </summary>
	/// <remarks>
	/// The content is explicitly forced onto the disk before the rename. Without that, the side
	/// file might sit in the operating system's write buffer alone: on a power cut the rename
	/// would then be done but the new file empty — and the old, intact version already
	/// replaced.
	/// </remarks>
	/// <param name="filePath">The path of the target file.</param>
	/// <param name="content">The text to write.</param>
	/// <exception cref="DataFileException">When the file cannot be written.</exception>
	public static void WriteTextAtomically(string filePath, string content)
	{
		string temporaryPath = filePath + TemporarySuffix;

		try
		{
			WriteAndFlush(temporaryPath, content);
			File.Move(temporaryPath, filePath, overwrite: true);
		}
		catch (Exception error) when (error is IOException or UnauthorizedAccessException)
		{
			DeleteLeftoverFile(temporaryPath);

			throw new DataFileException(
				$"Die Datei „{filePath}“ konnte nicht geschrieben werden: {error.Message}",
				error);
		}
	}

	/// <summary>
	/// Writes the text into the side file and forces it onto the disk.
	/// </summary>
	/// <param name="temporaryPath">The path of the side file.</param>
	/// <param name="content">The text to write.</param>
	private static void WriteAndFlush(string temporaryPath, string content)
	{
		using FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None);
		using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

		writer.Write(content);
		writer.Flush();
		stream.Flush(flushToDisk: true);
	}

	/// <summary>
	/// Removes a side file left behind, as far as that is possible.
	/// </summary>
	/// <remarks>
	/// If the deletion fails too, so be it: what gets reported is the original write error, not
	/// the failed cleanup. An orphaned side file is harmless — the next write attempt
	/// overwrites it.
	/// </remarks>
	/// <param name="temporaryPath">The path of the side file.</param>
	private static void DeleteLeftoverFile(string temporaryPath)
	{
		try
		{
			File.Delete(temporaryPath);
		}
		catch (Exception error) when (error is IOException or UnauthorizedAccessException)
		{
			// Deliberately swallowed: see the remark above.
		}
	}
}
