using System.Collections.Generic;
using System.Windows;
using Sessions.Core;

namespace Sessions.App;

public partial class AddWindowsDialog : Window
{
    private readonly AddWindowsViewModel _viewModel;

    public AddWindowsDialog(AddWindowsViewModel viewModel)
    {
        InitializeComponent();
        ThemeManager.ApplyToWindow(this);
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;

        Loaded += async (_, _) => await viewModel.LoadWindowsAsync();
    }

    // The windows the user chose, or null if cancelled
    public IReadOnlyList<WindowInfo>? SelectedWindows { get; private set; }

    private void OnCloseRequested()
    {
        SelectedWindows = _viewModel.SelectedWindows;
        DialogResult = SelectedWindows is not null;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
