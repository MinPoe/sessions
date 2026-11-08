using System.Management;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Sessions.Core;

// Reads what we can about a process behind a window: its executable path (Win32
// API) and its command line + parent pid (WMI). Each returns null on failure
// rather than throwing, so one unreadable process never sinks enumeration.
internal static class ProcessDetails
{
    private const int ERROR_ACCESS_DENIED = 5;

    // Full image path via the Win32 API. On failure, exePath is null and reason
    // explains why (no guessing a path). AccessDenied is true when the open was
    // refused — a strong hint the target runs elevated or is protected.
    public static (string? ExePath, string? Reason, bool AccessDenied) ReadExePath(uint processId)
    {
        using var handle = PInvoke.OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            bInheritHandle: false,
            processId);

        if (handle.IsInvalid)
        {
            bool denied = Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED;
            return (null, $"OpenProcess failed for pid {processId} (likely elevated or protected).", denied);
        }

        Span<char> buffer = new char[1024];
        uint size = (uint)buffer.Length;
        if (!PInvoke.QueryFullProcessImageName(handle, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref size))
        {
            return (null, $"QueryFullProcessImageName failed for pid {processId}.", false);
        }

        return (buffer[..(int)size].ToString(), null, false);
    }

    // CommandLine and ParentProcessId from Win32_Process. An empty command line
    // comes back as null; adapters decide whether that's an error.
    public static (string? CommandLine, uint? ParentProcessId) ReadWmi(uint processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine, ParentProcessId FROM Win32_Process WHERE ProcessId = {processId}");

            foreach (ManagementBaseObject mo in searcher.Get())
            {
                using (mo)
                {
                    string? commandLine = mo["CommandLine"] as string;
                    if (string.IsNullOrEmpty(commandLine))
                    {
                        commandLine = null;
                    }

                    uint? parentProcessId = mo["ParentProcessId"] is { } parent
                        ? Convert.ToUInt32(parent)
                        : null;

                    return (commandLine, parentProcessId);
                }
            }
        }
        catch (ManagementException)
        {
            // WMI unavailable or access denied — leave both unknown.
        }

        return (null, null);
    }
}
