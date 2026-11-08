using System.Collections.Generic;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Sessions.Core;

// Enumerates the "real" top-level windows: visible, titled, unowned, and not
// tool windows. These are the ones worth offering the user to capture/place.
public static class WindowEnumerator
{
    public static IReadOnlyList<WindowInfo> Enumerate()
    {
        List<WindowInfo> windows = new();

        PInvoke.EnumWindows(
            (HWND hwnd, LPARAM _) =>
            {
                if (TryDescribe(hwnd, out WindowInfo info))
                {
                    windows.Add(info);
                }

                return true; // keep enumerating
            },
            default);

        return windows;
    }

    private static bool TryDescribe(HWND hwnd, out WindowInfo info)
    {
        info = default!;

        if (!PInvoke.IsWindowVisible(hwnd))
        {
            return false;
        }

        // Skip owned windows (dialogs, tool palettes, etc.).
        if (!PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER).IsNull)
        {
            return false;
        }

        // Skip tool windows, which aren't meant to appear in the alt-tab list.
        // The extended style flags all fit in the low 32 bits, so GetWindowLong is enough.
        const uint WS_EX_TOOLWINDOW = 0x00000080;
        int exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (((uint)exStyle & WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        string title = GetTitle(hwnd);
        if (title.Length == 0)
        {
            return false;
        }

        uint processId;
        unsafe
        {
            _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);
        }

        info = new WindowInfo(hwnd, title, processId);
        return true;
    }

    private static string GetTitle(HWND hwnd)
    {
        int length = PInvoke.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        // +1 for the null terminator GetWindowText writes.
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
