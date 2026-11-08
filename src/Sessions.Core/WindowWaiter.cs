using System.Collections.Generic;
using System.Threading;

namespace Sessions.Core;

// Waits for a newly opened top-level window belonging to a given executable
public interface IWindowWaiter
{
    // Returns the handle of a window of exePath that wasn't already open, or
    // throws RestoreException if none appears within the timeout
    nint WaitForNewWindow(string exePath, string displayName);
}

public sealed class WindowWaiter : IWindowWaiter
{
    public const int TimeoutSeconds = 15;
    private const int PollIntervalMs = 200;

    public nint WaitForNewWindow(string exePath, string displayName)
    {
        // Snapshot the windows of this exe that already exist, so we only match
        // one that opens after launch
        HashSet<nint> existing = HandlesForExe(exePath);

        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(TimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            foreach ((var handle, uint pid) in WindowEnumerator.EnumerateVisibleHandles())
            {
                if (existing.Contains(handle))
                {
                    continue;
                }

                if (IsFromExe(pid, exePath))
                {
                    return handle;
                }
            }

            Thread.Sleep(PollIntervalMs);
        }

        throw new RestoreException($"{displayName}: window did not appear within {TimeoutSeconds} s");
    }

    private static HashSet<nint> HandlesForExe(string exePath)
    {
        HashSet<nint> result = new();
        foreach ((var handle, uint pid) in WindowEnumerator.EnumerateVisibleHandles())
        {
            if (IsFromExe(pid, exePath))
            {
                result.Add(handle);
            }
        }

        return result;
    }

    // Matches when target is a full path (exact match) or a bare file name like
    // "Code.exe" (match any process with that executable name)
    private static bool IsFromExe(uint pid, string target)
    {
        (string? candidate, _, _) = ProcessDetails.ReadExePath(pid);
        if (candidate is null)
        {
            return false;
        }

        bool targetIsPath = target.Contains('\\') || target.Contains('/');
        return targetIsPath
            ? string.Equals(candidate, target, StringComparison.OrdinalIgnoreCase)
            : string.Equals(System.IO.Path.GetFileName(candidate), target, StringComparison.OrdinalIgnoreCase);
    }
}
