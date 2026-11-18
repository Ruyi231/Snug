using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnugWin.App.Controllers;
using SnugWin.Core.Support;
using SnugWin.Windows.Support;

namespace SnugWin.App.Views;

public partial class SettingsWindow : Window
{
    private readonly AppController _controller;
    private bool _suppressEvents;
    private bool _isUpdating;

    public SettingsWindow(AppController controller, int initialTab = 0)
    {
        InitializeComponent();
        WindowPlacementHelper.ApplyModernDarkChrome(this);
        _controller = controller;
        _controller.UiStateChanged += () => Dispatcher.BeginInvoke(Refresh);
        _controller.UpdateStatusChanged += upd => Dispatcher.BeginInvoke(() => RefreshUpdateUi(upd));
        _controller.LogAppended += line => Dispatcher.BeginInvoke(() =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
        });

        LogBox.Text = string.Join(Environment.NewLine, _controller.GetRecentLogs()) + Environment.NewLine;
        LogBox.ScrollToEnd();
        Refresh();
        SelectTab(initialTab);
    }

    public void SelectTab(int index)
    {
        switch (index)
        {
            case 1: NavGestures.IsChecked = true; break;
            case 2: NavDiagnostics.IsChecked = true; break;
            case 3: NavAbout.IsChecked = true; break;
            default: NavGeneral.IsChecked = true; break;
        }
    }

    private void OnNavTabChanged(object sender, RoutedEventArgs e)
    {
        if (PageGeneral is null) return;
        PageGeneral.Visibility = NavGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageGestures.Visibility = NavGestures.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageDiagnostics.Visibility = NavDiagnostics.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = NavAbout.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Refresh()
    {
        _suppressEvents = true;
        try
        {
            VersionBadgeText.Text = $"v{_controller.CurrentVersion}";
            string pkgLabel = _controller.PackageKind == ReleasePackageKind.SelfContained ? "自包含版" : "框架依赖版";
            AboutVersionText.Text = $"当前版本：v{_controller.CurrentVersion}（{pkgLabel}）";

            StatusText.Text = _controller.GetStatusSummary();
            StatusIndicatorDot.Fill = _controller.IsPhoneScreenReady
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x34, 0xD3, 0x99))
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x60, 0xA5, 0xFA));

            bool pipeOk = _controller.IsDriverPipeAvailable;
            DiagPipeText.Text = pipeOk
                ? @"已连接命名管道 \\.\pipe\virtualdisplaydriver"
                : @"未检测到命名管道 \\.\pipe\virtualdisplaydriver";

            if (!pipeOk)
            {
                DriverWarningText.Text = @"未检测到 \\.\pipe\virtualdisplaydriver，请安装或重启 virtual-display-rs 驱动。";
                DriverWarningBanner.Visibility = Visibility.Visible;
            }
            else
            {
                DriverWarningBanner.Visibility = Visibility.Collapsed;
            }

            ElevationText.Text = _controller.IsElevated ? "管理员权限运行中" : "当前权限：普通用户";
            ElevateBtn.Visibility = _controller.IsElevated ? Visibility.Collapsed : Visibility.Visible;

            var cfg = _controller.Config;
            PhoneScreenCheck.IsChecked = cfg.PhoneScreen;
            AutoStartCheck.IsChecked = _controller.IsAutoStartEnabled;
            GesturesCheck.IsChecked = cfg.Gestures;
            GestureScrollCheck.IsChecked = cfg.GestureScroll;
            GestureRightClickCheck.IsChecked = cfg.GestureLongPressRightClick;
            CopyAfterSelectionCheck.IsChecked = cfg.CopyAfterSelection;
            InjectedOnlyCheck.IsChecked = cfg.GesturesInjectedOnly;
            GestureScrollCheck.IsEnabled = cfg.Gestures;
            GestureRightClickCheck.IsEnabled = cfg.Gestures;
            CopyAfterSelectionCheck.IsEnabled = cfg.Gestures;
            InjectedOnlyCheck.IsEnabled = cfg.Gestures;
            ScreenshotCheck.IsChecked = cfg.Screenshot;
            ManualPhoneCheck.IsChecked = _controller.ManualOverride;
            AutoCheckUpdatesCheck.IsChecked = cfg.AutoCheckUpdates;
            PhoneModelText.Text = $"{cfg.Phone.Model ?? "自定义"}（{cfg.Phone.Screen.Width} × {cfg.Phone.Screen.Height}）";

            foreach (ComboBoxItem item in ZoomCombo.Items)
            {
                if (item.Tag is string s && int.TryParse(s, out int val) && val == cfg.ZoomSteps)
                {
                    ZoomCombo.SelectedItem = item;
                    break;
                }
            }

            RefreshUpdateUi(_controller.LatestUpdate);
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void RefreshUpdateUi(UpdateCheckResult? upd)
    {
        if (_isUpdating) return;
        if (upd is null)
        {
            UpdateDot.Visibility = Visibility.Collapsed;
            InstallUpdateBtn.Visibility = Visibility.Collapsed;
            return;
        }

        if (upd.HasUpdate)
        {
            UpdateDot.Visibility = Visibility.Visible;
            UpdateStateText.Text = $"发现新版本 v{upd.LatestVersion}（点击可直接下载、校验 SHA-256 并重启升级）";
            InstallUpdateBtn.Visibility = string.IsNullOrWhiteSpace(upd.PackageDownloadUrl)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        else
        {
            UpdateDot.Visibility = Visibility.Collapsed;
            UpdateStateText.Text = $"已是最新版本（v{upd.CurrentVersion}）";
            InstallUpdateBtn.Visibility = Visibility.Collapsed;
        }
    }

    private void OnPhoneScreenChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) _controller.SetPhoneScreenEnabled(PhoneScreenCheck.IsChecked == true);
    }

    private void OnAutoStartChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        bool target = AutoStartCheck.IsChecked == true;
        try
        {
            _controller.SetAutoStartEnabled(target);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"设置开机自启动失败：{ex.Message}", "SnugWin", MessageBoxButton.OK, MessageBoxImage.Warning);
            Refresh();
        }
    }

    private void OnGesturesChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) _controller.SetGesturesEnabled(GesturesCheck.IsChecked == true);
    }

    private void OnGestureScrollChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) _controller.SetGestureScrollEnabled(GestureScrollCheck.IsChecked == true);
    }

    private void OnGestureRightClickChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) _controller.SetGestureLongPressRightClickEnabled(GestureRightClickCheck.IsChecked == true);
    }

    private void OnCopyAfterSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) _controller.SetCopyAfterSelectionEnabled(CopyAfterSelectionCheck.IsChecked == true);
    }

    private void OnInjectedOnlyChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) _controller.SetGesturesInjectedOnlyEnabled(InjectedOnlyCheck.IsChecked == true);
    }

    private void OnScreenshotChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) _controller.SetScreenshotEnabled(ScreenshotCheck.IsChecked == true);
    }

    private void OnManualChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) _controller.SetManualOverride(ManualPhoneCheck.IsChecked == true);
    }

    private void OnAutoCheckUpdatesChanged(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents) _controller.SetAutoCheckUpdatesEnabled(AutoCheckUpdatesCheck.IsChecked == true);
    }

    private void OnZoomChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        if (ZoomCombo.SelectedItem is ComboBoxItem { Tag: string s } && int.TryParse(s, out int val))
            _controller.SetDefaultZoomSteps(val);
    }

    private void OnOpenModels(object sender, RoutedEventArgs e)
    {
        new PhoneModelWindow(_controller) { Owner = this }.ShowDialog();
    }

    private void OnOpenLogDir(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = _controller.LogDirectory, UseShellExecute = true });
    }

    private void OnInstallDriver(object sender, RoutedEventArgs e)
    {
        try
        {
            _controller.LaunchDriverInstaller();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"启动驱动安装程序失败：{ex.Message}", "SnugWin", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        UpdateStateText.Text = "正在向 GitHub 查询最新版本...";
        try
        {
            var res = await _controller.CheckForUpdateAsync();
            RefreshUpdateUi(res);
        }
        catch (Exception ex)
        {
            UpdateStateText.Text = $"检查更新失败：{ex.Message}";
        }
        finally
        {
            CheckUpdateBtn.IsEnabled = true;
        }
    }

    private async void OnInstallUpdate(object sender, RoutedEventArgs e)
    {
        var upd = _controller.LatestUpdate;
        if (upd is null || !upd.HasUpdate || string.IsNullOrWhiteSpace(upd.PackageDownloadUrl))
            return;

        _isUpdating = true;
        CheckUpdateBtn.IsEnabled = false;
        InstallUpdateBtn.IsEnabled = false;
        UpdateProgressBar.Value = 0;
        UpdateProgressBar.Visibility = Visibility.Visible;
        UpdateStateText.Text = $"正在下载 v{upd.LatestVersion} ({upd.PackageAssetName})...";

        try
        {
            var progress = new Progress<double>(ratio =>
            {
                UpdateProgressBar.Value = ratio;
                UpdateStateText.Text = $"正在下载 v{upd.LatestVersion}... {ratio * 100:F0}%";
            });
            var prepared = await _controller.DownloadAndPrepareUpdateAsync(upd, progress, CancellationToken.None);
            UpdateStateText.Text = "校验通过，正在还原窗口并重启更新...";
            await _controller.ApplyPreparedUpdateAndRestartAsync(prepared);
        }
        catch (Exception ex)
        {
            _isUpdating = false;
            UpdateProgressBar.Visibility = Visibility.Collapsed;
            CheckUpdateBtn.IsEnabled = true;
            InstallUpdateBtn.IsEnabled = true;
            UpdateStateText.Text = $"更新失败：{ex.Message}";
            System.Windows.MessageBox.Show($"下载或校验更新失败：\n{ex.Message}", "更新失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnOpenRepo(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/Ruyi231/Snug",
            UseShellExecute = true
        });
    }

    private void OnRestartElevated(object sender, RoutedEventArgs e)
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas"
            });
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"无法以管理员启动：{ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
