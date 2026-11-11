using RafaoCluster.Services;
using System.Windows;

namespace RafaoCluster;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => DarkModeService.Apply((Window)sender)));
        base.OnStartup(e);
    }
}
