using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Sessions.Core;

// Enumerates the "real" top-level windows: visible, titled, unowned, not tool
// windows, not cloaked, not our own, and not the desktop shell, these are the
// ones worth offering the user to capture/place, enriched with process details
public static class WindowEnumerator
{
    public static IReadOnlyList<WindowInfo> Enumerate()
    {
        uint ownProcessId = (uint)Environment.ProcessId;
        List<WindowInfo> windows = new();

        PInvoke.EnumWindows(
            (HWND hwnd, LPARAM _) =>
            {
                if (TryDescribe(hwnd, ownProcessId, out WindowInfo info))
                {
                    windows.Add(info);
                }

                return true; // keep enumerating
            },
            default);

        return windows;
    }

    // Lightweight listing of visible top-level window handles with their pid,
    // without the process/WMI enrichment Enumerate() does, used for polling
    internal static List<(HWND Handle, uint ProcessId)> EnumerateVisibleHandles()
    {
        List<(HWND, uint)> handles = new();

        PInvoke.EnumWindows(
            (HWND hwnd, LPARAM _) =>
            {
                if (PInvoke.IsWindowVisible(hwnd))
                {
                    uint pid;
                    unsafe
                    {
                        PInvoke.GetWindowThreadProcessId(hwnd, &pid);
                    }

                    handles.Add((hwnd, pid));
                }

                return true;
            },
            default);

        return handles;
    }

    private static bool TryDescribe(HWND hwnd, uint ownProcessId, out WindowInfo info)
    {
        info = default!;

        if (!PInvoke.IsWindowVisible(hwnd))
        {
            return false;
        }

        // Skip owned windows (dialogs, tool palettes, etc.)
        if (!PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER).IsNull)
        {
            return false;
        }

        // Skip tool windows, which aren't meant to appear in the alt-tab list,
        // the extended style flags all fit in the low 32 bits, so GetWindowLong is enough
        const uint WS_EX_TOOLWINDOW = 0x00000080;
        int exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (((uint)exStyle & WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        // Skip app-cloaked windows (e.g. suspended UWP apps), but keep windows
        // cloaked only by the shell for being on another virtual desktop
        if (IsAppCloaked(hwnd))
        {
            return false;
        }

        string title = GetTitle(hwnd);
        if (title.Length == 0)
        {
            return false;
        }

        // Skip the desktop shell's own window
        if (title == "Program Manager")
        {
            return false;
        }

        uint processId;
        unsafe
        {
            _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);
        }

        // Skip our own windows
        if (processId == ownProcessId)
        {
            return false;
        }

        (string? exePath, string? reason, bool accessDenied) = ProcessDetails.ReadExePath(processId);
        (string? commandLine, uint? parentProcessId) = ProcessDetails.ReadWmi(processId);

        info = new WindowInfo(hwnd, title, processId)
        {
            ExePath = exePath,
            UnsupportedReason = reason,
            CommandLine = commandLine,
            ParentProcessId = parentProcessId,
            Placement = GetPlacement(hwnd),
            IsElevated = accessDenied,
        };

        // Let the classifier have the final say on why (if at all) it's unsupported
        info = info with { UnsupportedReason = WindowClassifier.GetUnsupportedReason(info) };
        return true;
    }

    private static Placement? GetPlacement(HWND hwnd)
    {
        WINDOWPLACEMENT placement = new() { length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!PInvoke.GetWindowPlacement(hwnd, ref placement))
        {
            return null;
        }

        RECT r = placement.rcNormalPosition;
        return new Placement(r.left, r.top, r.right - r.left, r.bottom - r.top, (int)placement.showCmd);
    }

    private static bool IsAppCloaked(HWND hwnd)
    {
        const uint DWM_CLOAKED_SHELL = 0x0000002; // cloaked by the shell (other virtual desktop)

        unsafe
        {
            uint cloaked;
            HRESULT hr = PInvoke.DwmGetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
                &cloaked,
                (uint)sizeof(uint));

            if (hr.Failed || cloaked == 0)
            {
                return false;
            }

            // Keep windows cloaked only because they're on another virtual desktop
            return cloaked != DWM_CLOAKED_SHELL;
        }
    }

    private static string GetTitle(HWND hwnd)
    {
        int length = PInvoke.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        // +1 for the null terminator GetWindowText writes
        Span<char> buffer = length < 256 ? stackalloc char[length + 1] : new char[length + 1];
        unsafe
        {
            fixed (char* p = buffer)
            {
                int copied = PInvoke.GetWindowText(hwnd, new PWSTR(p), buffer.Length);
                return new string(p, 0, copied);
            }
        }
    }
}
