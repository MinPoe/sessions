using System.ComponentModel;
using System.Windows;
using Sessions.Core;

namespace Sessions.App;

public partial class NewSessionDialog : Window
{
    private readonly NewSessionViewModel _viewModel;

    public NewSessionDialog(NewSessionViewModel viewModel)
    {
        InitializeComponent();
        ThemeManager.ApplyToWindow(this);
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += async (_, _) =>
        {
            NameBox.Focus();
            await viewModel.LoadWindowsAsync();
        };
    }

    // The session saved in the dialog, or null if cancelled
    public Session? SavedSession { get; private set; }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // After a partial capture the session is already saved; turn the primary
        // button into a plain "Done" that closes with the saved result
        if (e.PropertyName == nameof(NewSessionViewModel.IsSaved) && _viewModel.IsSaved)
        {
            PrimaryActionButton.Content = "Done";
            PrimaryActionButton.Command = _viewModel.CloseCommand;
            PrimaryActionButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Done");
        }
    }

    private void OnCloseRequested()
    {
        SavedSession = _viewModel.SavedSession;
        DialogResult = SavedSession is not null;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        // If a partial capture already saved a session, preserve it on close
        SavedSession = _viewModel.SavedSession;
        DialogResult = SavedSession is not null;
        Close();
    }
}
