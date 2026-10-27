using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Zeiterfassung.App;

/// <summary>
/// Sees to it that the app runs only once per Windows session.
/// </summary>
/// <remarks>
/// Two instances running at the same time would both write into the same <c>zeiten.json</c>
/// and read it independently; the second would overwrite the work of the first. The atomic
/// write is no protection against that — it prevents half files, not loss through an outdated
/// state. The scope is the Windows session and not the machine, because two signed-in users
/// have different data folders anyway: the folder path is kept per user and per machine.
/// </remarks>
internal sealed class SingleInstanceGuard : IDisposable
{
	/// <summary>
	/// The name of the lock object, session-wide in the „Local“ namespace.
	/// </summary>
	private const string MutexName = @"Local\Zeiterfassung.SingleInstance";

	/// <summary>
	/// The <c>ShowWindow</c> command that restores a minimized window.
	/// </summary>
	private const int RestoreCommand = 9;

	/// <summary>
	/// How often the window of the running instance is looked for.
	/// </summary>
	private const int SearchAttempts = 5;

	/// <summary>
	/// The wait in milliseconds between two search attempts.
	/// </summary>
	private const int WaitBetweenAttempts = 100;

	private readonly Mutex _mutex;

	/// <summary>
	/// Claims the lock of the Windows session.
	/// </summary>
	/// <remarks>
	/// All that counts is whether the lock object came into being anew. This way sidesteps what
	/// a crash leaves behind: if the first instance dies, Windows releases the lock object with
	/// it, and nothing stays around to be cleared away by hand.
	/// </remarks>
	public SingleInstanceGuard()
	{
		_mutex = new Mutex(initiallyOwned: true, MutexName, out bool isCreatedNew);

		IsOwner = isCreatedNew;
	}

	/// <summary>
	/// States whether this instance is the first and thereby the entitled one.
	/// </summary>
	public bool IsOwner { get; }

	/// <summary>
	/// Brings the window of the already running instance to the front.
	/// </summary>
	/// <remarks>
	/// If the running instance is still starting up — in the folder dialog of the first start,
	/// say — it may not have a main window yet. The search is therefore repeated and then given
	/// up on without a word: the lock has served its purpose by then, only the courtesy fails.
	/// </remarks>
	public static void ActivateRunningInstance()
	{
		for (int attempt = 0; attempt < SearchAttempts; attempt++)
		{
			IntPtr windowHandle = FindWindowOfRunningInstance();

			if (windowHandle != IntPtr.Zero)
			{
				ShowWindow(windowHandle, RestoreCommand);
				SetForegroundWindow(windowHandle);

				return;
			}

			Thread.Sleep(WaitBetweenAttempts);
		}
	}

	/// <summary>
	/// Releases the lock again.
	/// </summary>
	public void Dispose()
	{
		if (IsOwner)
		{
			ReleaseOwnership();
		}

		_mutex.Dispose();
	}

	/// <summary>
	/// Looks once for the main window of the other instance in the same session.
	/// </summary>
	/// <returns>The window that was found, or <c>IntPtr.Zero</c> when there is none.</returns>
	private static IntPtr FindWindowOfRunningInstance()
	{
		using Process ownProcess = Process.GetCurrentProcess();

		Process[] candidates = Process.GetProcessesByName(ownProcess.ProcessName);

		try
		{
			foreach (Process candidate in candidates)
			{
				if (candidate.Id == ownProcess.Id || candidate.SessionId != ownProcess.SessionId)
				{
					continue;
				}

				if (candidate.MainWindowHandle != IntPtr.Zero)
				{
					return candidate.MainWindowHandle;
				}
			}
		}
		finally
		{
			foreach (Process candidate in candidates)
			{
				candidate.Dispose();
			}
		}

		return IntPtr.Zero;
	}

	/// <summary>
	/// Releases the lock object, as far as that is possible.
	/// </summary>
	/// <remarks>
	/// A failure is deliberately swallowed: Windows releases the lock object anyway once the
	/// process ends. An error message while shutting down would help nobody.
	/// </remarks>
	private void ReleaseOwnership()
	{
		try
		{
			_mutex.ReleaseMutex();
		}
		catch (ApplicationException)
		{
			// Deliberately swallowed: see the remark above.
		}
	}

	/// <summary>
	/// Brings a window to the foreground (Windows interface <c>user32.dll</c>).
	/// </summary>
	/// <param name="windowHandle">The window to raise.</param>
	/// <returns>True when the window came to the foreground.</returns>
	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(IntPtr windowHandle);

	/// <summary>
	/// Changes the display state of a window (Windows interface <c>user32.dll</c>).
	/// </summary>
	/// <param name="windowHandle">The window concerned.</param>
	/// <param name="command">The wanted state, here always the restore.</param>
	/// <returns>True when the window was visible before.</returns>
	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool ShowWindow(IntPtr windowHandle, int command);
}
