using System;
using System.Windows;
using System.Windows.Input;

namespace CctqMonitor;

public partial class SettingsWindow : Window
{
    public AppConfig Config { get; private set; }

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();

        Config = new AppConfig
        {
            BaseUrl = config.BaseUrl,
            AccessToken = config.AccessToken,
            UserId = config.UserId,
            WindowLeft = config.WindowLeft,
            WindowTop = config.WindowTop,
            IsLocked = config.IsLocked,
            IsTopmost = config.IsTopmost
        };

        TokenBox.Password = Config.AccessToken;
        UserIdBox.Text = Config.UserId;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var baseUrl = "https://www.cctq.ai";
        var token = TokenBox.Password.Trim();
        var userId = UserIdBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            ErrorText.Text = "请输入系统访问令牌。";
            return;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            ErrorText.Text = "请输入用户 ID。";
            return;
        }

        Config.BaseUrl = baseUrl;
        Config.AccessToken = token;
        Config.UserId = userId;

        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Shell_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
