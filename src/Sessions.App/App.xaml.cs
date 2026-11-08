using System.Windows;

namespace Sessions.App;

// Application entry point, resources (theme) are merged in App.xaml; the color
// appearance follows the Windows system setting via ThemeManager
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeManager.Shutdown();
        base.OnExit(e);
    }
}
