using System.Diagnostics;
using System.IO;

namespace Sessions.Core;

// Captures and restores a VS Code window by its open folder or .code-workspace,
// resolved from globalStorage windowsState plus the window title, see
// docs/spikes/vscode.md, registered before GenericAdapter so Code.exe windows
// go here first
public sealed class VsCodeAdapter : IAdapter
{
    private const string CodeExeName = "Code.exe";

    private readonly Func<string?> _readStorageJson;
    private readonly IWindowWaiter _waiter;
    private readonly IWindowPlacer _placer;
    private readonly Action<string> _launch;

    public VsCodeAdapter()
        : this(ReadDefaultStorage, new WindowWaiter(), new WindowPlacer(), LaunchCode) { }

    public VsCodeAdapter(
        Func<string?> readStorageJson,
        IWindowWaiter waiter,
        IWindowPlacer placer,
        Action<string> launch)
    {
        _readStorageJson = readStorageJson ?? throw new ArgumentNullException(nameof(readStorageJson));
        _waiter = waiter ?? throw new ArgumentNullException(nameof(waiter));
        _placer = placer ?? throw new ArgumentNullException(nameof(placer));
        _launch = launch ?? throw new ArgumentNullException(nameof(launch));
    }

    // %APPDATA%\Code\User\globalStorage\storage.json
    public static string DefaultStoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Code", "User", "globalStorage", "storage.json");

    public string Id => "vscode";

    // Handles supported Code.exe windows
    public bool CanHandle(WindowInfo window) =>
        window.UnsupportedReason is null && IsCodeExe(window.ExePath);

    public Entry Capture(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        string? json = _readStorageJson();
        var windows = json is null
            ? Array.Empty<VsCodeWindow>()
            : VsCodeStorage.ParseWindows(json);

        string? path = VsCodeStorage.Resolve(window.Title, windows);
        if (path is null)
        {
            throw new CaptureException(
                $"{window.Title}: couldn't resolve the VS Code folder from windowsState.");
        }

        return new VsCodeEntry(DisplayNameFor(path), window.Placement, path);
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
