using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Sessions.Core;

// Captures and restores a VS Code window by its open folder or .code-workspace,
// resolved from globalStorage windowsState plus the window title, see
// docs/spikes/vscode.md, registered before GenericAdapter so Code.exe windows
// go here first
public sealed class VsCodeAdapter : IAdapter
{
    private const string CodeExeName = "Code.exe";

    private readonly Func<string?> _readStorageJson;
    private readonly Func<string?> _readRecentJson;
    private readonly IWindowWaiter _waiter;
    private readonly IWindowPlacer _placer;
    private readonly Action<string> _launch;

    public VsCodeAdapter()
        : this(ReadDefaultStorage, ReadDefaultRecentPaths, new WindowWaiter(), new WindowPlacer(), LaunchCode) { }

    public VsCodeAdapter(
        Func<string?> readStorageJson,
        Func<string?> readRecentJson,
        IWindowWaiter waiter,
        IWindowPlacer placer,
        Action<string> launch)
    {
        _readStorageJson = readStorageJson ?? throw new ArgumentNullException(nameof(readStorageJson));
        _readRecentJson = readRecentJson ?? throw new ArgumentNullException(nameof(readRecentJson));
        _waiter = waiter ?? throw new ArgumentNullException(nameof(waiter));
        _placer = placer ?? throw new ArgumentNullException(nameof(placer));
        _launch = launch ?? throw new ArgumentNullException(nameof(launch));
    }

    // %APPDATA%\Code\User\globalStorage\storage.json
    public static string DefaultStoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Code", "User", "globalStorage", "storage.json");

    // %APPDATA%\Code\User\globalStorage\state.vscdb, the SQLite store that holds the
    // recently opened list
    public static string DefaultStateDbPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Code", "User", "globalStorage", "state.vscdb");

    public string Id => "vscode";

    // Handles supported Code.exe windows
    public bool CanHandle(WindowInfo window) =>
        window.UnsupportedReason is null && IsCodeExe(window.ExePath);

    public Entry Capture(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // windowsState is precise but written lazily (often stale or empty), try it
        // first, then fall back to the recently opened list which is more current
        string? storageJson = _readStorageJson();
        var windowsStateWindows = storageJson is null
            ? Array.Empty<VsCodeWindow>()
            : VsCodeStorage.ParseWindows(storageJson);

        string? path = VsCodeStorage.Resolve(window.Title, windowsStateWindows);

        if (path is null)
        {
            string? recentJson = _readRecentJson();
            if (recentJson is not null)
            {
                path = VsCodeStorage.Resolve(window.Title, VsCodeStorage.ParseRecentFolders(recentJson));
            }
        }

        if (path is null)
        {
            throw new CaptureException(
                $"{window.Title}: couldn't resolve the VS Code folder from windowsState or the recent list.");
        }

        return new VsCodeEntry(DisplayNameFor(path), window.Placement, path)
        {
            IconSourcePath = window.ExePath,
        };
    }

    public void Restore(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry is not VsCodeEntry vsCode)
        {
            throw new RestoreException($"{entry.DisplayName}: VsCodeAdapter cannot restore a {entry.GetType().Name}.");
        }

        if (string.IsNullOrEmpty(vsCode.Path))
        {
            throw new RestoreException($"{vsCode.DisplayName}: no path to open.");
        }

        try
        {
            _launch(vsCode.Path);
        }
        catch (Exception ex)
        {
            throw new RestoreException($"{vsCode.DisplayName}: failed to launch VS Code for '{vsCode.Path}'.", ex);
        }

        nint handle = _waiter.WaitForNewWindow(CodeExeName, vsCode.DisplayName);

        if (vsCode.Placement is not null)
        {
            _placer.Apply(handle, vsCode.Placement, targetElevated: false);
        }
    }

    private static bool IsCodeExe(string? exePath) =>
        exePath is not null &&
        string.Equals(Path.GetFileName(exePath), CodeExeName, StringComparison.OrdinalIgnoreCase);

    private static string DisplayNameFor(string path) =>
        path.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(path) + " (Workspace)"
            : Path.GetFileName(path.TrimEnd('\\', '/'));

    private static string? ReadDefaultStorage()
    {
        try
        {
            return File.Exists(DefaultStoragePath) ? File.ReadAllText(DefaultStoragePath) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    // Reads the recently opened list value out of state.vscdb, the DB is open and
    // WAL-mode under a running VS Code, so copy it to a temp file and read the copy
    // (a consistent snapshot of the main file) rather than locking the live DB
    private const string RecentPathsKey = "history.recentlyOpenedPathsList";

    private static string? ReadDefaultRecentPaths()
    {
        if (!File.Exists(DefaultStateDbPath))
        {
            return null;
        }

        string temp = Path.Combine(Path.GetTempPath(), $"sessions-vscdb-{Guid.NewGuid():N}.db");
        try
        {
            File.Copy(DefaultStateDbPath, temp, overwrite: true);

            using var connection = new SqliteConnection($"Data Source={temp};Mode=ReadOnly");
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM ItemTable WHERE key = $key";
            command.Parameters.AddWithValue("$key", RecentPathsKey);

            return command.ExecuteScalar() as string;
        }
        catch (Exception ex) when (ex is IOException or SqliteException)
        {
            return null;
        }
        finally
        {
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
                // Temp cleanup is best-effort
            }
        }
    }

    // Opens a new window on the path via the code CLI shim, the cmd wrapper lets
    // us run code.cmd without a visible console
    private static void LaunchCode(string path)
    {
        ProcessStartInfo startInfo = new("cmd.exe", $"/c code -n \"{path}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Process.Start(startInfo);
    }
}
