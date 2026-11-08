using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sessions.Core;

// Captures and restores a Windows Terminal window with its tabs, each tab's folder
// comes from the hook records (TerminalRecords), a tab running Claude Code resumes
// its session and an ssh tab re-runs its command, see docs/spikes/terminal.md,
// registered before GenericAdapter so WindowsTerminal.exe windows go here first
public sealed class TerminalAdapter : IAdapter
{
    private const string WindowsTerminalExe = "WindowsTerminal.exe";

    private readonly Func<ProcessSnapshot> _snapshot;
    private readonly Func<IReadOnlyList<TerminalRecord>> _readRecords;
    private readonly Func<string, string?> _findClaudeSession;
    private readonly IWindowWaiter _waiter;
    private readonly IWindowPlacer _placer;
    private readonly Action<string> _launch;

    public TerminalAdapter()
        : this(
            ProcessSnapshot.Capture,
            () => TerminalRecords.Read(TerminalRecords.DefaultDirectory),
            cwd => ClaudeSession.FindSessionId(ClaudeSession.DefaultProjectsDirectory, cwd),
            new WindowWaiter(),
            new WindowPlacer(),
            LaunchWt)
    { }

    public TerminalAdapter(
        Func<ProcessSnapshot> snapshot,
        Func<IReadOnlyList<TerminalRecord>> readRecords,
        Func<string, string?> findClaudeSession,
        IWindowWaiter waiter,
        IWindowPlacer placer,
        Action<string> launch)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _readRecords = readRecords ?? throw new ArgumentNullException(nameof(readRecords));
        _findClaudeSession = findClaudeSession ?? throw new ArgumentNullException(nameof(findClaudeSession));
        _waiter = waiter ?? throw new ArgumentNullException(nameof(waiter));
        _placer = placer ?? throw new ArgumentNullException(nameof(placer));
        _launch = launch ?? throw new ArgumentNullException(nameof(launch));
    }

    public string Id => "terminal";

    public bool CanHandle(WindowInfo window) =>
        window.UnsupportedReason is null && IsWindowsTerminal(window.ExePath);

    public Entry Capture(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        ProcessSnapshot snapshot = _snapshot();
        IReadOnlyList<TerminalRecord> shells = TerminalRecords.LiveShells(_readRecords(), snapshot);

        if (shells.Count == 0)
        {
            throw new CaptureException(
                $"{window.Title}: no live shells were recorded, is the Sessions terminal hook installed and loaded in this shell?");
        }

        List<TerminalTab> tabs = new();
        foreach (TerminalRecord shell in shells)
        {
            (bool isClaude, string? sshCommand) = TerminalShellInspector.Inspect(snapshot, (uint)shell.Pid);

            string? claudeSessionId = null;
            if (isClaude)
            {
                claudeSessionId = _findClaudeSession(shell.Cwd)
                    ?? throw new CaptureException(
                        $"{window.Title}: Claude Code is running in {shell.Cwd} but no session file was found to resume.");
            }

            tabs.Add(new TerminalTab(shell.Cwd, shell.ShellPath, sshCommand, claudeSessionId));
        }

        return new TerminalEntry(DisplayNameFor(tabs), window.Placement, tabs);
    }

    public void Restore(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry is not TerminalEntry terminal)
        {
            throw new RestoreException($"{entry.DisplayName}: TerminalAdapter cannot restore a {entry.GetType().Name}.");
        }

        string arguments = WtCommandBuilder.Build(terminal);

        try
        {
            _launch(arguments);
        }
        catch (Exception ex)
        {
            throw new RestoreException($"{terminal.DisplayName}: failed to launch Windows Terminal.", ex);
        }

        nint handle = _waiter.WaitForNewWindow(WindowsTerminalExe, terminal.DisplayName);

        if (terminal.Placement is not null)
        {
            _placer.Apply(handle, terminal.Placement, targetElevated: false);
        }
    }

    private static bool IsWindowsTerminal(string? exePath) =>
        exePath is not null &&
        string.Equals(Path.GetFileName(exePath), WindowsTerminalExe, StringComparison.OrdinalIgnoreCase);

    private static string DisplayNameFor(IReadOnlyList<TerminalTab> tabs)
    {
        string first = Path.GetFileName(tabs[0].Cwd.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(first))
        {
            first = tabs[0].Cwd;
        }

        return tabs.Count == 1 ? $"Terminal — {first}" : $"Terminal — {first} +{tabs.Count - 1}";
    }

    // Start wt.exe through the shell so the Store execution alias resolves, the
    // arguments carry their own ; tab separators
    private static void LaunchWt(string arguments)
    {
        ProcessStartInfo startInfo = new("wt.exe", arguments) { UseShellExecute = true };
        Process.Start(startInfo);
    }
}
