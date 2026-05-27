using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CctqMonitor;

public partial class MainWindow : Window
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    private readonly ConfigStore _configStore;
    private readonly CctqApiClient _apiClient = new();
    private readonly DispatcherTimer _timer;
    private readonly Forms.NotifyIcon _notifyIcon;
    private Forms.ToolStripMenuItem _trayLockItem;
    private Forms.ToolStripMenuItem _trayTopmostItem;
    private AppConfig _config;
    private CancellationTokenSource _refreshCts;
    private bool _hasSuccessfulSnapshot;
    private bool _isRefreshing;

    public MainWindow(ConfigStore configStore, AppConfig config)
    {
        _configStore = configStore;
        _config = config;

        InitializeComponent();

        Left = _config.WindowLeft ?? 80;
        Top = _config.WindowTop ?? 80;
        Topmost = _config.IsTopmost;
        LockMenuItem.IsChecked = _config.IsLocked;
        TopmostMenuItem.IsChecked = _config.IsTopmost;

        _notifyIcon = CreateNotifyIcon();
        _timer = new DispatcherTimer { Interval = RefreshInterval };
        _timer.Tick += async (_, _) => await RefreshAsync();

        Loaded += MainWindow_OnLoaded;
        LocationChanged += MainWindow_OnLocationChanged;
        Closing += MainWindow_OnClosing;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_config.HasCredentials)
        {
            OpenSettings();
        }

        _timer.Start();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        _refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            StatusText.Text = "刷新中";

            var snapshot = await _apiClient.FetchSnapshotAsync(_config, _refreshCts.Token);
            ApplySnapshot(snapshot);
        }
        finally
        {
            _refreshCts.Dispose();
            _refreshCts = null;
            _isRefreshing = false;
        }
    }

    private void ApplySnapshot(MonitorSnapshot snapshot)
    {
        if (snapshot.Light != ConnectionLight.Gray)
        {
            _hasSuccessfulSnapshot = true;
            BalanceText.Text = FormatAmount(snapshot.Balance);
            TodayUsageText.Text = FormatAmount(snapshot.TodayUsage);
        }
        else if (!_hasSuccessfulSnapshot)
        {
            BalanceText.Text = "0.00";
            TodayUsageText.Text = "0.00";
        }

        ConnectionDot.Fill = new SolidColorBrush(GetLightColor(snapshot.Light));
        LatencyText.Text = snapshot.Latency.HasValue
            ? snapshot.Latency.Value.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + "s"
            : "";
        StatusText.Text = snapshot.StatusText;
        _notifyIcon.Text = $"CCTQ Monitor - {snapshot.StatusText}";
    }

    private static string FormatAmount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static System.Windows.Media.Color GetLightColor(ConnectionLight light) => light switch
    {
        ConnectionLight.Green => System.Windows.Media.Color.FromRgb(52, 199, 89),
        ConnectionLight.Yellow => System.Windows.Media.Color.FromRgb(255, 204, 0),
        ConnectionLight.Red => System.Windows.Media.Color.FromRgb(255, 59, 48),
        _ => System.Windows.Media.Color.FromRgb(161, 161, 166)
    };

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("刷新", null, async (_, _) => await RefreshAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());

        _trayLockItem = new Forms.ToolStripMenuItem("锁定位置") { CheckOnClick = true, Checked = _config.IsLocked };
        _trayLockItem.Click += (_, _) =>
        {
            _config.IsLocked = _trayLockItem.Checked;
            LockMenuItem.IsChecked = _trayLockItem.Checked;
            SaveConfig();
        };
        menu.Items.Add(_trayLockItem);

        _trayTopmostItem = new Forms.ToolStripMenuItem("置顶") { CheckOnClick = true, Checked = _config.IsTopmost };
        _trayTopmostItem.Click += (_, _) =>
        {
            _config.IsTopmost = _trayTopmostItem.Checked;
            TopmostMenuItem.IsChecked = _trayTopmostItem.Checked;
            Topmost = _trayTopmostItem.Checked;
            SaveConfig();
        };
        menu.Items.Add(_trayTopmostItem);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(CloseApplication));

        var icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location) ?? SystemIcons.Application,
            Text = "CCTQ Monitor",
            Visible = true,
            ContextMenuStrip = menu
        };

        icon.DoubleClick += (_, _) => Dispatcher.Invoke(() =>
        {
            Show();
            Activate();
        });

        return icon;
    }

    private void Shell_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_config.IsLocked)
        {
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Shell_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        Shell.ContextMenu!.IsOpen = true;
    }

    private async void RefreshMenuItem_OnClick(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void LockMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        _config.IsLocked = LockMenuItem.IsChecked;
        if (_trayLockItem != null)
        {
            _trayLockItem.Checked = _config.IsLocked;
        }
        SaveConfig();
    }

    private void TopmostMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        _config.IsTopmost = TopmostMenuItem.IsChecked;
        Topmost = _config.IsTopmost;
        if (_trayTopmostItem != null)
        {
            _trayTopmostItem.Checked = _config.IsTopmost;
        }
        SaveConfig();
    }

    private void SettingsMenuItem_OnClick(object sender, RoutedEventArgs e) => OpenSettings();

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e) => CloseApplication();

    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(_config)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true)
        {
            _config = settingsWindow.Config;
            ApplyWindowConfig();
            SaveConfig();
            _ = RefreshAsync();
        }
    }

    private void ApplyWindowConfig()
    {
        Topmost = _config.IsTopmost;
        LockMenuItem.IsChecked = _config.IsLocked;
        TopmostMenuItem.IsChecked = _config.IsTopmost;
        if (_trayLockItem != null)
        {
            _trayLockItem.Checked = _config.IsLocked;
        }
        if (_trayTopmostItem != null)
        {
            _trayTopmostItem.Checked = _config.IsTopmost;
        }
    }

    private void MainWindow_OnLocationChanged(object sender, EventArgs e)
    {
        _config.WindowLeft = Left;
        _config.WindowTop = Top;
        SaveConfig();
    }

    private void SaveConfig() => _configStore.Save(_config);

    private void MainWindow_OnClosing(object sender, CancelEventArgs e)
    {
        _refreshCts?.Cancel();
        _timer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _apiClient.Dispose();
    }

    private void CloseApplication()
    {
        SaveConfig();
        Application.Current.Shutdown();
    }
}
