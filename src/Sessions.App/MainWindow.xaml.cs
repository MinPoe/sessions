using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;
using Sessions.Core;

namespace Sessions.App;

// Main window, implements IShell so the view model can drive dialogs and file
// pickers without referencing views directly
public partial class MainWindow : Window, IShell
{
    public MainWindow()
    {
        InitializeComponent();
        ThemeManager.ApplyToWindow(this);
        DataContext = new MainViewModel(this);

        RestoreWindowPlacement();
        Closing += OnClosing;
    }

    // Reopen at the remembered size and spot, ignoring a saved rectangle that would
    // land the window off every screen (e.g. a monitor since unplugged)
    private void RestoreWindowPlacement()
    {
        if (WindowSettings.Load() is not { } s)
        {
            return;
        }

        if (s.Width >= MinWidth && s.Height >= MinHeight && IsOnScreen(s))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = s.Left;
            Top = s.Top;
            Width = s.Width;
            Height = s.Height;
        }

        if (s.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private static bool IsOnScreen(WindowSettings s)
    {
        // Keep at least a sliver of the title bar within the virtual desktop
        double vx = SystemParameters.VirtualScreenLeft;
        double vy = SystemParameters.VirtualScreenTop;
        double vr = vx + SystemParameters.VirtualScreenWidth;
        double vb = vy + SystemParameters.VirtualScreenHeight;
        return s.Left + s.Width > vx + 40 && s.Left < vr - 40 &&
               s.Top + 40 < vb && s.Top >= vy - 1;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        bool maximized = WindowState == WindowState.Maximized;

        // RestoreBounds gives the normal (un-maximized) rectangle to come back to
        Rect r = maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
        new WindowSettings(r.Left, r.Top, r.Width, r.Height, maximized).Save();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    // Select the row under a right-click so the context menu acts on it
    private void OnEntryRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListViewItem item)
        {
            item.IsSelected = true;
        }
    }

    public Session? ShowNewSessionDialog()
    {
        JsonSessionStore store = new(JsonSessionStore.DefaultDirectory);
        AdapterRegistry registry = new(
            new IAdapter[] { new VsCodeAdapter(), new TerminalAdapter() },
            new GenericAdapter());

        NewSessionDialog dialog = new(new NewSessionViewModel(store, registry)) { Owner = this };
        dialog.ShowDialog();
        return dialog.SavedSession;
    }

    public IReadOnlyList<WindowInfo>? ShowAddWindowsDialog()
    {
        AdapterRegistry registry = new(
            new IAdapter[] { new VsCodeAdapter(), new TerminalAdapter() },
            new GenericAdapter());

        AddWindowsDialog dialog = new(new AddWindowsViewModel(registry)) { Owner = this };
        return dialog.ShowDialog() == true ? dialog.SelectedWindows : null;
    }

    public void ShowRestoreResults(string sessionName, IReadOnlyList<RestoreResult> results)
    {
        RestoreResultDialog dialog = new(sessionName, results) { Owner = this };
        dialog.ShowDialog();
    }

    public bool Confirm(string title, string message, string confirmLabel, bool destructive)
    {
        ConfirmDialog dialog = new(title, message, confirmLabel, "Cancel", destructive) { Owner = this };
        return dialog.ShowDialog() == true;
    }

    public void ShowMessage(string title, string message)
    {
        ConfirmDialog dialog = new(title, message, "OK", cancelLabel: null, destructive: false) { Owner = this };
        dialog.ShowDialog();
    }

    public string? AskSavePath(string suggestedFileName)
    {
        SaveFileDialog dialog = new()
        {
            Title = "Export Session",
            FileName = suggestedFileName,
            Filter = "Session file (*.json)|*.json",
            DefaultExt = ".json",
            AddExtension = true,
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    public string? AskOpenPath()
    {
        OpenFileDialog dialog = new()
        {
            Title = "Import Session",
            Filter = "Session file (*.json)|*.json",
            CheckFileExists = true,
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }
}
