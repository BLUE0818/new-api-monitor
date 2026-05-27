using System.Windows;

namespace CctqMonitor;

public partial class App : Application
{
    private MainWindow _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configStore = new ConfigStore();
        var config = configStore.Load();
        _mainWindow = new MainWindow(configStore, config);
        MainWindow = _mainWindow;
        _mainWindow.Show();
    }
}
