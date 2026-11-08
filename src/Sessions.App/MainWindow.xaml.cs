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
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

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
