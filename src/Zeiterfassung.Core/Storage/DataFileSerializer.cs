using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zeiterfassung.Core.Storage;

/// <summary>
/// Reads and writes the data files as JSON — shared by all three.
/// </summary>
/// <remarks>
/// Gathers what holds equally for every file: the serializer options, the atomic write, and
/// the turning of technical errors into a <see cref="DataFileException"/> with a plain German
/// message. Were this present per file, the same rule would stand in three places — and could
/// drift apart in three.
/// </remarks>
internal static class DataFileSerializer
{
	/// <summary>
	/// The serializer options for both directions.
	/// </summary>
	/// <remarks>
	/// <c>UnmappedMemberHandling.Disallow</c> implements the rule that unknown fields are not
	/// tolerated: what the app does not know it does not interpret, it rejects the file. An
	/// extension of the format therefore costs a bump of the format version.
	/// </remarks>
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = false,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = true,
	};

	/// <summary>
	/// Reads a data file and turns it into its image.
	/// </summary>
	/// <typeparam name="TContent">The image class of the file.</typeparam>
	/// <param name="filePath">The path of the file.</param>
	/// <returns>The image of the file.</returns>
	/// <exception cref="DataFileException">When the file is missing, unreadable, or does not match the expected shape.</exception>
	public static TContent ReadFile<TContent>(string filePath)
	{
		string content = ReadFileContent(filePath);

		try
		{
			return JsonSerializer.Deserialize<TContent>(content, SerializerOptions)
				?? throw new DataFileException($"Die Datei „{filePath}“ ist leer.");
		}
		catch (JsonException error)
		{
			throw new DataFileException($"Die Datei „{filePath}“ ist nicht lesbar: {error.Message}", error);
		}
	}

	/// <summary>
	/// Writes an image to disk as a data file.
	/// </summary>
	/// <remarks>
	/// Always atomically, through <see cref="AtomicFileWriter"/>: a crash mid-write must leave
	/// no half file behind.
	/// </remarks>
	/// <typeparam name="TContent">The image class of the file.</typeparam>
	/// <param name="filePath">The path of the file.</param>
	/// <param name="content">The image to write.</param>
	/// <exception cref="DataFileException">When the file cannot be written.</exception>
	public static void WriteFile<TContent>(string filePath, TContent content)
	{
		AtomicFileWriter.WriteTextAtomically(filePath, JsonSerializer.Serialize(content, SerializerOptions));
	}

	/// <summary>
	/// Checks the format version of a file for an exact match.
	/// </summary>
	/// <remarks>
	/// A missing entry and a wrong number are both rejected, but with different messages — the
	/// user is meant to know what to do.
	/// </remarks>
	/// <param name="filePath">The path of the file, for the message.</param>
	/// <param name="formatVersion">The version found in the file, or null.</param>
	/// <param name="supportedVersion">The version this app expects.</param>
	/// <exception cref="DataFileException">When the version is missing or does not match.</exception>
	public static void RequireSupportedVersion(string filePath, int? formatVersion, int supportedVersion)
	{
		if (formatVersion is null)
		{
			throw new DataFileException(
				$"Die Datei „{filePath}“ nennt keine Formatversion und wird deshalb nicht gedeutet.");
		}

		if (formatVersion != supportedVersion)
		{
			throw new DataFileException(
				$"Die Datei „{filePath}“ hat die Formatversion {formatVersion}, "
				+ $"erwartet wird {supportedVersion}. Sie wird nicht gedeutet.");
		}
	}

	/// <summary>
	/// Returns the message of an exception in the form that can be put before the user.
	/// </summary>
	/// <remarks>
	/// .NET appends a hint about the parameter concerned to the message of an
	/// <see cref="ArgumentException"/> by itself — something that helps the developer and only
	/// confuses the user. The cut is made at the bracket at the end and not at the word
	/// „Parameter“, because that word differs with the Windows language pack.
	/// </remarks>
	/// <param name="error">The exception whose message is to be shown.</param>
	/// <returns>The message without the technical tail.</returns>
	public static string DescribeCause(Exception error)
	{
		if (error is not ArgumentException argumentError || argumentError.ParamName is null)
		{
			return error.Message;
		}

		int hintStart = error.Message.LastIndexOf(" (", StringComparison.Ordinal);

		return hintStart > 0 && error.Message.EndsWith(')')
			? error.Message[..hintStart]
			: error.Message;
	}

	/// <summary>
	/// Reads the text of a file from disk.
	/// </summary>
	/// <param name="filePath">The path of the file.</param>
	/// <returns>The contents of the file.</returns>
	/// <exception cref="DataFileException">When the file is missing or cannot be read.</exception>
	private static string ReadFileContent(string filePath)
	{
		try
		{
			return File.ReadAllText(filePath);
		}
		catch (FileNotFoundException error)
		{
			throw new DataFileException($"Die Datei „{filePath}“ wurde nicht gefunden.", error);
		}
		catch (DirectoryNotFoundException error)
		{
			throw new DataFileException($"Der Ordner zur Datei „{filePath}“ wurde nicht gefunden.", error);
		}
		catch (Exception error) when (error is IOException or UnauthorizedAccessException)
		{
			throw new DataFileException(
				$"Die Datei „{filePath}“ konnte nicht gelesen werden: {error.Message}",
				error);
		}
	}
}
