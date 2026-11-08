using System.Diagnostics;

namespace Sessions.Core;

// The catch-all adapter: launches an executable with its captured command-line
// arguments and restores its window placement.
public sealed class GenericAdapter : IAdapter
{
    private readonly IWindowWaiter _waiter;
    private readonly IWindowPlacer _placer;

    public GenericAdapter() : this(new WindowWaiter(), new WindowPlacer()) { }

    public GenericAdapter(IWindowWaiter waiter, IWindowPlacer placer)
    {
        _waiter = waiter ?? throw new ArgumentNullException(nameof(waiter));
        _placer = placer ?? throw new ArgumentNullException(nameof(placer));
    }

    public string Id => "generic";

    // Handles any window we consider supported.
    public bool CanHandle(WindowInfo window) => window.UnsupportedReason is null;

    public Entry Capture(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (string.IsNullOrEmpty(window.ExePath))
        {
            throw new CaptureException($"{window.Title}: no executable path to capture.");
        }

        string arguments = ArgumentsFromCommandLine(window.CommandLine, window.ExePath);
        return new GenericEntry(window.Title, window.Placement, window.ExePath, arguments);
    }

    public void Restore(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry is not GenericEntry generic)
        {
            throw new RestoreException($"{entry.DisplayName}: GenericAdapter cannot restore a {entry.GetType().Name}.");
        }

        if (string.IsNullOrEmpty(generic.ExePath))
        {
            throw new RestoreException($"{generic.DisplayName}: no executable path to launch.");
        }

        ProcessStartInfo startInfo = new(generic.ExePath, generic.Arguments)
        {
            UseShellExecute = false,
        };

        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            throw new RestoreException($"{generic.DisplayName}: failed to launch '{generic.ExePath}'.", ex);
        }

        nint handle = _waiter.WaitForNewWindow(generic.ExePath, generic.DisplayName);

        if (generic.Placement is not null)
        {
            _placer.Apply(handle, generic.Placement, targetElevated: false);
        }
    }

    // A captured command line is "<exe>" <args>. Strip the leading executable
    // token and return the remainder; empty when there were no arguments.
    private static string ArgumentsFromCommandLine(string? commandLine, string exePath)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return string.Empty;
        }

        string trimmed = commandLine.TrimStart();
        int cut;

        if (trimmed.StartsWith('"'))
        {
            int closing = trimmed.IndexOf('"', 1);
            cut = closing < 0 ? trimmed.Length : closing + 1;
        }
        else
        {
            int space = trimmed.IndexOf(' ');
            cut = space < 0 ? trimmed.Length : space;
        }

        return trimmed[cut..].TrimStart();
    }
}
