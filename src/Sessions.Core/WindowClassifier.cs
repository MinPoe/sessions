using System.IO;

namespace Sessions.Core;

// Decides whether a window can be captured/placed, and if not, says why,
// pure function of WindowInfo so it's easy to unit-test
public static class WindowClassifier
{
    // Returns a human-readable reason the window is unsupported, or null if it's fine
    public static string? GetUnsupportedReason(WindowInfo window)
    {
        if (window.IsElevated)
        {
            return "Runs elevated; can't be captured unless Sessions also runs as administrator.";
        }

        string? exePath = window.ExePath;
        if (exePath is null)
        {
            // Carry forward whatever stopped us reading the process
            return window.UnsupportedReason ?? "The owning process could not be inspected.";
        }

        string fileName = Path.GetFileName(exePath);

        if (string.Equals(fileName, "explorer.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "File Explorer and shell windows aren't supported.";
        }

        if (string.Equals(fileName, "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "UWP app-host windows aren't supported (the real app lives in another process).";
        }

        return null;
    }
}
