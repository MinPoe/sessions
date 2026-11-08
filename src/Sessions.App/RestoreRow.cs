using System.Windows.Media.Imaging;
using Sessions.Core;

namespace Sessions.App;

// A single entry's restore outcome, shown in the result dialog
public sealed class RestoreRow
{
    public RestoreRow(RestoreResult result)
    {
        DisplayName = result.Entry.DisplayName;
        Success = result.Success;
        StatusText = result.Success
            ? "Restored"
            : result.ErrorMessage ?? "Could not be restored.";

        // The app's own icon, from the exe this entry was captured from; fall back to
        // a generic entry's ExePath for sessions saved before IconSourcePath existed
        Icon = IconLoader.ForExecutable(
            result.Entry.IconSourcePath ?? (result.Entry as GenericEntry)?.ExePath);
    }

    public string DisplayName { get; }

    public bool Success { get; }

    public string StatusText { get; }

    // A glyph that doubles the status so it isn't conveyed by color alone
    public string Glyph => Success ? "✓" : "✕";

    public BitmapSource? Icon { get; }
}
