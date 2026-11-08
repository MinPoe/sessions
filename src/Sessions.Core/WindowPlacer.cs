using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Sessions.Core;

// A rectangle by edges, matching Win32 RECT semantics
public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

// Applies a captured Placement to a live window
public interface IWindowPlacer
{
    // Positions hwnd per placement, throws RestoreException if the target is elevated
    void Apply(nint hwnd, Placement placement, bool targetElevated);
}

public sealed class WindowPlacer : IWindowPlacer
{
    // Pure, testable: shrink to fit and slide a (possibly off-screen) rectangle
    // back onto the work area, preserving size where it fits
    public static Placement ClampToWorkArea(Placement placement, ScreenRect workArea)
    {
        int width = Math.Min(placement.Width, workArea.Width);
        int height = Math.Min(placement.Height, workArea.Height);

        int x = Math.Clamp(placement.X, workArea.Left, workArea.Right - width);
        int y = Math.Clamp(placement.Y, workArea.Top, workArea.Bottom - height);

        return placement with { X = x, Y = y, Width = width, Height = height };
    }

    public void Apply(nint hwnd, Placement placement, bool targetElevated)
    {
        if (targetElevated)
        {
            throw new RestoreException("cannot position elevated window; run sessions as administrator");
        }

        Placement clamped = ClampToWorkArea(placement, PrimaryWorkArea());

        WINDOWPLACEMENT wp = new()
        {
            length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>(),
            showCmd = (SHOW_WINDOW_CMD)clamped.ShowCmd,
            rcNormalPosition = new RECT
            {
                left = clamped.X,
                top = clamped.Y,
                right = clamped.X + clamped.Width,
                bottom = clamped.Y + clamped.Height,
            },
        };

        PInvoke.SetWindowPlacement((HWND)hwnd, in wp);
    }

    private static ScreenRect PrimaryWorkArea()
    {
        RECT work = default;
        unsafe
        {
            _ = PInvoke.SystemParametersInfo(
                SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETWORKAREA,
                0,
                &work,
                default); // reading only, no update broadcast
        }

        return new ScreenRect(work.left, work.top, work.right, work.bottom);
    }
}
