using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace OpenFleetIT.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ApplicationItem> _applications = [];

    public ICollectionView ApplicationsView { get; }

    public MainWindow()
    {
        var settings = SettingsStore.LoadAsync().GetAwaiter().GetResult();
        LocalizationService.Apply(settings.Language);
        InitializeComponent();
        ApplicationsView = CollectionViewSource.GetDefaultView(_applications);
        DataContext = this;
        SourceInitialized += (_, _) => EnableSystemBackdrop();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (ApplicationsView is null) return;

        var query = SearchBox.Text.Trim();
        ApplicationsView.Filter = item =>
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            if (item is not ApplicationItem application) return false;
            return application.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || application.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || application.Version.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || application.Status.Contains(query, StringComparison.OrdinalIgnoreCase);
        };
    }

    private void AppAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.DataContext is not ApplicationItem application)
            return;

        var actionKey = button.Tag?.ToString() switch
        {
            "Repair" => "Repair",
            "Update" => "Update",
            "Uninstall" => "Uninstall",
            _ => "Actions"
        };

        MessageBox.Show(
            string.Format(LocalizationService.Text("ActionDemoMessage"), LocalizationService.Text(actionKey), application.Name),
            LocalizationService.Text("ActionDemoTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
        ApplicationsView.Refresh();
        AppsGrid.Items.Refresh();
    }

    private void OpenFleetScan_Click(object sender, RoutedEventArgs e)
    {
        new FleetScanWindow { Owner = this }.ShowDialog();
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var target = ConnectionTargetInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(target) || target.Length > 255 || target.Any(char.IsWhiteSpace))
        {
            MessageBox.Show(LocalizationService.Text("InvalidTargetMessage"), LocalizationService.Text("InvalidTargetTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConnectButton.IsEnabled = false;
        ConnectionStatusLabel.Text = string.Format(LocalizationService.Text("Connecting"), target.ToUpperInvariant());
        try
        {
            var information = await PcInfoService.GetAsync(target);
            var uptime = DateTime.Now - information.LastBoot;
            var firewallText = information.FirewallEnabled switch
            {
                true => LocalizationService.Text("Enabled"),
                false => LocalizationService.Text("Disabled"),
                null => LocalizationService.Text("Unknown")
            };
            var restartText = information.RestartPending switch
            {
                true => LocalizationService.Text("Yes"),
                false => LocalizationService.Text("No"),
                null => LocalizationService.Text("Unknown")
            };

            DeviceTitleLabel.Text = information.Hostname.ToUpperInvariant();
            SelectedDeviceLabel.Text = $"  /  {information.Hostname}";
            ConnectionStatusLabel.Text = $"{information.Manufacturer} {information.Model} · {LocalizationService.Text("Connected")}".Trim();
            WindowsVersionLabel.Text = information.WindowsCaption.Replace("Microsoft ", string.Empty, StringComparison.OrdinalIgnoreCase);
            WindowsBuildLabel.Text = string.Format(LocalizationService.Text("BuildFormat"), information.WindowsVersion, information.BuildNumber);
            UptimeLabel.Text = string.Format(LocalizationService.Text("UptimeFormat"), Math.Max(0, uptime.Days), Math.Max(0, uptime.Hours));
            BootDateLabel.Text = string.Format(LocalizationService.Text("BootDateFormat"), information.LastBoot);
            FirewallStatusLabel.Text = firewallText;
            FirewallDetailsLabel.Text = information.FirewallProfileCount > 0
                ? string.Format(LocalizationService.Text("FirewallProfilesFormat"), information.FirewallProfileCount)
                : LocalizationService.Text("InformationUnavailable");
            RestartStatusLabel.Text = restartText;
            RestartReasonLabel.Text = information.RestartPending switch
            {
                true => LocalizationService.Text("SystemRegistry"),
                false => LocalizationService.Text("NoRestartPending"),
                null => LocalizationService.Text("InformationUnavailable")
            };

            await LoadSoftwareInventoryAsync(target);
        }
        catch (Exception exception)
        {
            ConnectionStatusLabel.Text = LocalizationService.Text("ConnectionFailedTitle").ToUpperInvariant();
            MessageBox.Show(
                string.Format(LocalizationService.Text("ConnectionFailedMessage"), target, exception.Message),
                LocalizationService.Text("ConnectionFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private async void RefreshInventory_Click(object sender, RoutedEventArgs e)
    {
        var target = ConnectionTargetInput.Text.Trim();
        if (!string.IsNullOrWhiteSpace(target))
            await LoadSoftwareInventoryAsync(target);
    }

    private async Task LoadSoftwareInventoryAsync(string target)
    {
        InventoryCountLabel.Text = LocalizationService.Text("InventoryLoading");
        try
        {
            var packages = await SoftwareInventoryService.GetAsync(target);
            _applications.Clear();
            foreach (var package in packages)
            {
                _applications.Add(new ApplicationItem(
                    package.Name,
                    package.Publisher,
                    package.Version,
                    "Installed",
                    "#2457D7A3",
                    "#FF57D7A3",
                    package.CanRepair,
                    false,
                    package.CanUninstall));
            }

            InventoryCountLabel.Text = string.Format(LocalizationService.Text("InventoryCountFormat"), packages.Count);
        }
        catch
        {
            _applications.Clear();
            InventoryCountLabel.Text = LocalizationService.Text("InventoryUnavailable");
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void EnableSystemBackdrop()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        var hwnd = new WindowInteropHelper(this).Handle;
        const int darkModeAttribute = 20;
        const int systemBackdropAttribute = 38;
        var enabled = 1;
        var backdropType = 2;
        _ = DwmSetWindowAttribute(hwnd, darkModeAttribute, ref enabled, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, systemBackdropAttribute, ref backdropType, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}

public sealed record ApplicationItem(string Name, string Publisher, string Version, string StatusResourceKey,
    string StatusBackgroundHex, string StatusForegroundHex, bool CanRepair, bool CanUpdate, bool CanUninstall)
{
    public string Status => LocalizationService.Text(StatusResourceKey);
    public Brush StatusBackground => new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusBackgroundHex));
    public Brush StatusForeground => new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusForegroundHex));
}
