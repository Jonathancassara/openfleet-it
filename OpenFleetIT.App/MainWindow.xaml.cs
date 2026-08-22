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
    public ObservableCollection<WingetUpdate> Updates { get; } = [];
    public ObservableCollection<InstalledDriver> Drivers { get; } = [];
    private string? _connectedTarget;

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
        ShowCentralPanel(CentralPage.Settings);
        ApplicationsView.Refresh();
        AppsGrid.Items.Refresh();
    }

    private void OpenFleetScan_Click(object sender, RoutedEventArgs e)
    {
        ShowCentralPanel(CentralPage.Fleet);
    }

    private void OpenCommands_Click(object sender, RoutedEventArgs e)
    {
        ShowCentralPanel(CentralPage.Commands);
        CommandTargetLabel.Text = string.IsNullOrWhiteSpace(_connectedTarget)
            ? LocalizationService.Text("NoConnectedDevice")
            : _connectedTarget;
    }

    private void OpenUpdates_Click(object sender, RoutedEventArgs e) => ShowCentralPanel(CentralPage.Updates);

    private void OpenDrivers_Click(object sender, RoutedEventArgs e) => ShowCentralPanel(CentralPage.Drivers);

    private async void RefreshDrivers_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_connectedTarget))
        {
            DriversStatusLabel.Text = LocalizationService.Text("ConnectBeforeDrivers");
            return;
        }

        RefreshDriversButton.IsEnabled = false;
        DriversStatusLabel.Text = LocalizationService.Text("ReadingDrivers");
        Drivers.Clear();
        try
        {
            var drivers = await DriverInventoryService.GetAsync(_connectedTarget);
            foreach (var driver in drivers) Drivers.Add(driver);
            DriversStatusLabel.Text = string.Format(LocalizationService.Text("DriversFoundFormat"), Drivers.Count, _connectedTarget);
        }
        catch (Exception exception)
        {
            DriversStatusLabel.Text = string.Format(LocalizationService.Text("DriversErrorFormat"), exception.Message);
        }
        finally
        {
            RefreshDriversButton.IsEnabled = true;
        }
    }

    private async void CheckSoftwareUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_connectedTarget))
        {
            UpdatesStatusLabel.Text = LocalizationService.Text("ConnectBeforeUpdates");
            return;
        }

        if (!IsLocalTarget(_connectedTarget))
        {
            UpdatesStatusLabel.Text = LocalizationService.Text("RemoteUpdatesUnavailable");
            return;
        }

        CheckSoftwareUpdatesButton.IsEnabled = false;
        UpdatesStatusLabel.Text = LocalizationService.Text("CheckingSoftwareUpdates");
        Updates.Clear();
        try
        {
            var result = await WingetUpdateService.GetAvailableUpdatesAsync();
            if (!result.IsWingetAvailable)
            {
                UpdatesStatusLabel.Text = LocalizationService.Text("WingetUnavailable");
                return;
            }

            foreach (var update in result.Updates) Updates.Add(update);
            UpdatesStatusLabel.Text = result.Error is not null
                ? string.Format(LocalizationService.Text("WingetErrorFormat"), result.Error)
                : string.Format(LocalizationService.Text("UpdatesFoundFormat"), Updates.Count);
        }
        finally
        {
            CheckSoftwareUpdatesButton.IsEnabled = true;
        }
    }

    private void OpenWorkstation_Click(object sender, RoutedEventArgs e) => ShowCentralPanel(CentralPage.Workstation);

    private void ShowCentralPanel(CentralPage page)
    {
        CommandsPanel.Visibility = page == CentralPage.Commands ? Visibility.Visible : Visibility.Collapsed;
        FleetPanel.Visibility = page == CentralPage.Fleet ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = page == CentralPage.Settings ? Visibility.Visible : Visibility.Collapsed;
        UpdatesPanel.Visibility = page == CentralPage.Updates ? Visibility.Visible : Visibility.Collapsed;
        DriversPanel.Visibility = page == CentralPage.Drivers ? Visibility.Visible : Visibility.Collapsed;
        var deviceVisibility = page == CentralPage.Workstation && !string.IsNullOrWhiteSpace(_connectedTarget)
            ? Visibility.Visible
            : Visibility.Collapsed;
        DeviceHeaderPanel.Visibility = deviceVisibility;
        DeviceSummaryPanel.Visibility = deviceVisibility;
        DeviceDetailsPanel.Visibility = deviceVisibility;
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

            _connectedTarget = target;
            if (CommandsPanel.Visibility == Visibility.Visible)
                CommandTargetLabel.Text = target;
            else if (FleetPanel.Visibility != Visibility.Visible && SettingsPanel.Visibility != Visibility.Visible && UpdatesPanel.Visibility != Visibility.Visible && DriversPanel.Visibility != Visibility.Visible)
                ShowCentralPanel(CentralPage.Workstation);

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

    private static bool IsLocalTarget(string target) => target.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || target.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
        || target is "127.0.0.1" or "::1" or ".";

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

internal enum CentralPage
{
    Workstation,
    Commands,
    Fleet,
    Settings,
    Updates,
    Drivers
}

public sealed record ApplicationItem(string Name, string Publisher, string Version, string StatusResourceKey,
    string StatusBackgroundHex, string StatusForegroundHex, bool CanRepair, bool CanUpdate, bool CanUninstall)
{
    public string Status => LocalizationService.Text(StatusResourceKey);
    public Brush StatusBackground => new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusBackgroundHex));
    public Brush StatusForeground => new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusForegroundHex));
}
