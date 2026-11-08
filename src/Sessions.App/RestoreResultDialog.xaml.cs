using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Sessions.Core;

namespace Sessions.App;

public partial class RestoreResultDialog : Window
{
    public RestoreResultDialog(string sessionName, IReadOnlyList<RestoreResult> results)
    {
        InitializeComponent();
        ThemeManager.ApplyToWindow(this);

        DataContext = results.Select(r => new RestoreRow(r)).ToList();

        int total = results.Count;
        int ok = results.Count(r => r.Success);

        HeaderBlock.Text = sessionName;
        SummaryBlock.Text = ok == total
            ? (total == 1 ? "1 app restored." : $"All {total} apps restored.")
            : $"{ok} of {total} apps restored.";
    }

    private void OnDone(object sender, RoutedEventArgs e) => Close();
}
