using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Sessions.App;

// Loads an executable's associated icon as a WPF ImageSource, best-effort:
// returns null when the icon can't be read, so callers show a placeholder
public static class IconLoader
{
    public static BitmapSource? ForExecutable(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return null;
        }

        try
        {
            using Icon? icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null)
            {
                return null;
            }

            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze(); // make it usable across threads / cacheable
            return source;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
