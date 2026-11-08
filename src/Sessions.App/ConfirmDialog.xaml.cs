using System.Windows;

namespace Sessions.App;

// A small confirmation / message dialog, with a cancel label it confirms an
// action; without one it's a single-button message
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmLabel, string? cancelLabel, bool destructive)
    {
        InitializeComponent();
        ThemeManager.ApplyToWindow(this);

        Title = title;
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        ConfirmButton.Content = confirmLabel;

        if (cancelLabel is null)
        {
            CancelButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            CancelButton.Content = cancelLabel;
        }

        if (destructive)
        {
            ConfirmButton.Style = (Style)FindResource("DestructiveButton");
        }

        // Accessible names mirror the visible labels
        ConfirmButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, confirmLabel);
        if (cancelLabel is not null)
        {
            CancelButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, cancelLabel);
        }
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
