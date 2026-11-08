using System.Windows.Media.Imaging;
using Sessions.Core;

namespace Sessions.App;

// A candidate window in the new-session checklist
public sealed class WindowRow : ObservableObject
{
    private bool _isSelected;

    public WindowRow(WindowInfo window, string? adapterId)
    {
        Window = window;
        Adapter = adapterId;
        Icon = IconLoader.ForExecutable(window.ExePath);
    }

    public WindowInfo Window { get; }

    public string Title => string.IsNullOrWhiteSpace(Window.Title) ? "(untitled)" : Window.Title;

    // The adapter that would capture this window, or null if none can
    public string? Adapter { get; }

    public bool IsSupported => Window.UnsupportedReason is null;

    public string? Reason => Window.UnsupportedReason;

    public BitmapSource? Icon { get; }

    // Only supported windows can be checked
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (IsSupported)
            {
                SetProperty(ref _isSelected, value);
            }
        }
    }
}
