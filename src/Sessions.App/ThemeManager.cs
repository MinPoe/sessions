using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Sessions.App;

public enum AppTheme { Light, Dark }

// Follows the Windows system appearance: picks light/dark at startup, swaps the
// color dictionary live when the user changes their system setting, and matches
// each window's title bar to the theme, no in-app toggle (per Apple HIG, the app
// doesn't offer its own appearance switch)
public static class ThemeManager
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    // Call once at startup
    public static void Initialize()
    {
        Apply(DetectSystemTheme());
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Shutdown()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    // Matches a window's title bar to the current theme; call when it's shown
    public static void ApplyToWindow(Window window)
    {
        void Set()
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            int dark = Current == AppTheme.Dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            Set();
        }
        else
        {
            window.SourceInitialized += (_, _) => Set();
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            Apply(DetectSystemTheme());
            foreach (Window window in Application.Current.Windows)
            {
                ApplyToWindow(window);
            }
        }
    }

    private static void Apply(AppTheme theme)
    {
        Current = theme;

        string source = theme == AppTheme.Dark ? "Themes/DarkColors.xaml" : "Themes/LightColors.xaml";
        ResourceDictionary colors = new() { Source = new Uri(source, UriKind.Relative) };

        // Index 0 is the color appearance (see App.xaml)
        Application.Current.Resources.MergedDictionaries[0] = colors;
    }

    private static AppTheme DetectSystemTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            // AppsUseLightTheme: 1 = light, 0 = dark
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0 ? AppTheme.Dark : AppTheme.Light;
            }
        }
        catch (Exception)
        {
            // Fall back to light if the setting can't be read
        }

        return AppTheme.Light;
    }
}
