using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using OpenFleetIT.Core;

namespace OpenFleetIT.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ApplicationItem> _applications = [];
    public ObservableCollection<WingetUpdate> Updates { get; } = [];
    public ObservableCollection<InstalledDriver> Drivers { get; } = [];
    public ObservableCollection<ActionLogEntry> ActionEntries { get; } = [];
    private string? _connectedTarget;
    private PcInformation? _lastPcInformation;
    private CancellationTokenSource? _activeActionCancellation;

    public ICollectionView ApplicationsView { get; }

    public MainWindow()
    {
        var settings = SettingsStore.LoadAsync().GetAwaiter().GetResult();
        LocalizationService.Apply(settings.Language);
        InitializeComponent();
        ApplicationsView = CollectionViewSource.GetDefaultView(_applications);
        DataContext = this;
        PopulateCurrentUser();
        SettingsPanel.LanguageChanged += (_, _) => PopulateCurrentUser();
        HomeHelperPanel.InventoryConnected += HomeHelperPanel_InventoryConnected;
        SourceInitialized += (_, _) => EnableSystemBackdrop();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (ApplicationsView is null) return;
        ApplyApplicationFilter(SearchBox.Text);
    }

    private void ApplicationsSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        ApplyApplicationFilter(ApplicationsSearchInput.Text);

    private void ApplyApplicationFilter(string text)
    {
        var query = text.Trim();
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

    private void OpenApplications_Click(object sender, RoutedEventArgs e) => ShowCentralPanel(CentralPage.Applications);

    private void OpenSecurity_Click(object sender, RoutedEventArgs e) => ShowCentralPanel(CentralPage.Security);

    private void OpenHomeDevices_Click(object sender, RoutedEventArgs e) => ShowCentralPanel(CentralPage.Home);

    private async void OpenActionLog_Click(object sender, RoutedEventArgs e)
    {
        ShowCentralPanel(CentralPage.ActionLog);
        await LoadActionLogAsync();
    }

    private async void RefreshActionLog_Click(object sender, RoutedEventArgs e) => await LoadActionLogAsync();

    private async Task LoadActionLogAsync()
    {
        ActionEntries.Clear();
        var entries = await ActionLogService.ReadAsync();
        foreach (var entry in entries.OrderByDescending(item => item.Timestamp)) ActionEntries.Add(entry);
        ActionLogStatusLabel.Text = entries.Count == 0
            ? LocalizationService.Text("ActionLogEmpty")
            : string.Format(LocalizationService.Text("ActionLogCountFormat"), entries.Count,
                entries.All(entry => entry.IntegrityValid) ? LocalizationService.Text("Verified") : LocalizationService.Text("Invalid"));
    }

    private async void AppAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.DataContext is not ApplicationItem application)
            return;

        if (string.IsNullOrWhiteSpace(_connectedTarget) || !IsLocalTarget(_connectedTarget))
        {
            MessageBox.Show(LocalizationService.Text("LocalSoftwareActionsOnly"), LocalizationService.Text("ActionBlocked"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var repair = button.Tag?.ToString() == "Repair";
        var actionKey = repair ? "Repair" : "Uninstall";
        if (string.IsNullOrWhiteSpace(application.ProductCode)) return;
        var preview = SoftwareActionService.PreviewMsi(application.ProductCode, repair);
        if (MessageBox.Show(string.Format(LocalizationService.Text("SoftwareActionConfirmation"), LocalizationService.Text(actionKey), application.Name, preview),
                LocalizationService.Text("ConfirmAction"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        if (!TryBeginAction(out var cancellationToken)) return;
        button.IsEnabled = false;
        try
        {
            var result = await SoftwareActionService.ExecuteMsiAsync(application.ProductCode, repair, cancellationToken);
            await SafeLogAsync("Software", _connectedTarget, $"{actionKey} {application.Name}", result.Success ? "Success" : "Error",
                $"ExitCode={result.ExitCode}; {result.Details}");
            MessageBox.Show(string.Format(LocalizationService.Text("ActionResultFormat"), result.Success ? LocalizationService.Text("ActionSucceeded") : LocalizationService.Text("ActionFailed"), result.ExitCode, result.Details),
                LocalizationService.Text("ActionResult"), MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            if (result.Success) await LoadSoftwareInventoryAsync(_connectedTarget);
        }
        finally
        {
            button.IsEnabled = true;
            FinishAction();
        }
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
        var connected = !string.IsNullOrWhiteSpace(_connectedTarget);
        LogOffCommandButton.IsEnabled = connected && IsLocalTarget(_connectedTarget!);
        ShutdownCommandButton.IsEnabled = connected;
        RebootCommandButton.IsEnabled = connected;
    }

    private async void WingetUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.DataContext is not WingetUpdate update
            || string.IsNullOrWhiteSpace(_connectedTarget) || !IsLocalTarget(_connectedTarget)) return;
        var executable = WingetUpdateService.FindExecutable();
        if (executable is null)
        {
            MessageBox.Show(LocalizationService.Text("WingetUnavailable"), LocalizationService.Text("ActionBlocked"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var preview = SoftwareActionService.PreviewWinget(update.Id);
        if (MessageBox.Show(string.Format(LocalizationService.Text("SoftwareActionConfirmation"), LocalizationService.Text("Update"), update.Name, preview),
                LocalizationService.Text("ConfirmAction"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (!TryBeginAction(out var cancellationToken)) return;
        button.IsEnabled = false;
        try
        {
            var result = await SoftwareActionService.ExecuteWingetAsync(executable, update.Id, cancellationToken);
            await SafeLogAsync("Software", _connectedTarget, $"Update {update.Id}", result.Success ? "Success" : "Error",
                $"ExitCode={result.ExitCode}; {result.Details}");
            MessageBox.Show(string.Format(LocalizationService.Text("ActionResultFormat"), result.Success ? LocalizationService.Text("ActionSucceeded") : LocalizationService.Text("ActionFailed"), result.ExitCode, result.Details),
                LocalizationService.Text("ActionResult"), MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            if (result.Success) CheckSoftwareUpdates_Click(CheckSoftwareUpdatesButton, new RoutedEventArgs());
        }
        finally
        {
            button.IsEnabled = true;
            FinishAction();
        }
    }

    private bool TryBeginAction(out CancellationToken cancellationToken)
    {
        if (_activeActionCancellation is not null)
        {
            MessageBox.Show(LocalizationService.Text("ActionAlreadyRunning"), LocalizationService.Text("ActionBlocked"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            cancellationToken = default;
            return false;
        }
        _activeActionCancellation = new CancellationTokenSource();
        CancelApplicationActionButton.IsEnabled = true;
        CancelUpdateActionButton.IsEnabled = true;
        cancellationToken = _activeActionCancellation.Token;
        return true;
    }

    private void FinishAction()
    {
        _activeActionCancellation?.Dispose();
        _activeActionCancellation = null;
        CancelApplicationActionButton.IsEnabled = false;
        CancelUpdateActionButton.IsEnabled = false;
    }

    private void CancelActiveAction_Click(object sender, RoutedEventArgs e)
    {
        _activeActionCancellation?.Cancel();
        SoftwareActionService.CancelAll();
    }

    private async void ExportInventory_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_connectedTarget) || _applications.Count == 0)
        {
            MessageBox.Show(LocalizationService.Text("InventoryNotLoaded"), LocalizationService.Text("ExportInventory"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var json = sender is System.Windows.Controls.Button button && button.Tag?.ToString() == "json";
        var safeTarget = string.Concat(_connectedTarget.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"OpenFleet-{safeTarget}-{DateTime.Now:yyyyMMdd-HHmm}",
            DefaultExt = json ? ".json" : ".csv",
            Filter = json ? "JSON (*.json)|*.json" : "CSV (*.csv)|*.csv"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (json) await InventoryExportService.ExportJsonAsync(dialog.FileName, _connectedTarget, _applications);
            else await InventoryExportService.ExportCsvAsync(dialog.FileName, _applications);
            MessageBox.Show(string.Format(LocalizationService.Text("ExportCompleteFormat"), dialog.FileName),
                LocalizationService.Text("ExportInventory"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(string.Format(LocalizationService.Text("ExportFailedFormat"), exception.Message),
                LocalizationService.Text("ExportInventory"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void PowerCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || string.IsNullOrWhiteSpace(_connectedTarget)) return;
        var action = button.Name switch
        {
            "LogOffCommandButton" => PowerAction.LogOff,
            "ShutdownCommandButton" => PowerAction.Shutdown,
            "RebootCommandButton" => PowerAction.Reboot,
            _ => PowerAction.Abort
        };
        var actionName = LocalizationService.Text(action switch
        {
            PowerAction.LogOff => "LogOff",
            PowerAction.Shutdown => "ShutDown",
            PowerAction.Reboot => "Reboot",
            _ => "CancelScheduled"
        });
        if (action != PowerAction.Abort && MessageBox.Show(
                string.Format(LocalizationService.Text("PowerActionConfirmation"), actionName, _connectedTarget),
                LocalizationService.Text("ConfirmAction"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        button.IsEnabled = false;
        var result = await SoftwareActionService.ExecutePowerCommandAsync(_connectedTarget, action);
        await SafeLogAsync("Power", _connectedTarget, action.ToString(), result.Success ? "Success" : "Error",
            $"ExitCode={result.ExitCode}; {result.Details}");
        MessageBox.Show(string.Format(LocalizationService.Text("ActionResultFormat"), result.Success ? LocalizationService.Text("ActionSucceeded") : LocalizationService.Text("ActionFailed"), result.ExitCode, result.Details),
            LocalizationService.Text("ActionResult"), MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        button.IsEnabled = true;
    }

    private void BrowsePsExec_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.Text("SelectPsExec"),
            Filter = "PsExec (PsExec.exe)|PsExec.exe|Executable (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            PsExecPathInput.Text = dialog.FileName;
            PsExecStatusLabel.Text = LocalizationService.Text("PsExecReadyToTest");
        }
    }

    private async void TestPsExec_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_connectedTarget))
        {
            PsExecStatusLabel.Text = LocalizationService.Text("NoConnectedDevice");
            return;
        }
        if (string.IsNullOrWhiteSpace(PsExecPathInput.Text))
        {
            PsExecStatusLabel.Text = LocalizationService.Text("SelectPsExecFirst");
            return;
        }

        var confirmation = MessageBox.Show(
            string.Format(LocalizationService.Text("PsExecProbeConfirmation"), _connectedTarget),
            LocalizationService.Text("PsExecConnector"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

        TestPsExecButton.IsEnabled = false;
        PsExecStatusLabel.Text = LocalizationService.Text("TestingPsExec");
        try
        {
            var result = await PsExecConnectorService.TestAsync(PsExecPathInput.Text, _connectedTarget);
            PsExecStatusLabel.Foreground = result.Success ? (Brush)FindResource("Success") : (Brush)FindResource("Danger");
            PsExecStatusLabel.Text = result.Details;
            await SafeLogAsync("Remote", _connectedTarget, "PsExec probe", result.Success ? "Success" : "Error", result.Details);
        }
        finally
        {
            TestPsExecButton.IsEnabled = true;
        }
    }

    private void OpenUpdates_Click(object sender, RoutedEventArgs e) => ShowCentralPanel(CentralPage.Updates);

    private void OpenDrivers_Click(object sender, RoutedEventArgs e) => ShowCentralPanel(CentralPage.Drivers);

    private async void RefreshSecurity_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_connectedTarget))
        {
            SecurityStatusLabel.Text = LocalizationService.Text("ConnectBeforeSecurity");
            return;
        }

        RefreshSecurityButton.IsEnabled = false;
        SecurityStatusLabel.Text = LocalizationService.Text("ReadingSecurity");
        try
        {
            var snapshot = await SecurityInventoryService.GetAsync(_connectedTarget);
            DefenderValue.Text = FormatState(snapshot.DefenderEnabled);
            DefenderDetail.Text = snapshot.SignatureAgeDays is int age
                ? string.Format(LocalizationService.Text("SignatureAgeFormat"), age)
                : LocalizationService.Text("InformationUnavailable");
            BitLockerValue.Text = FormatState(snapshot.BitLockerProtected);
            TpmValue.Text = snapshot.TpmPresent == false ? LocalizationService.Text("NotPresent") : FormatState(snapshot.TpmEnabled);
            SecureBootValue.Text = FormatState(snapshot.SecureBootEnabled);
            SecurityFirewallValue.Text = FormatState(_lastPcInformation?.FirewallEnabled);
            SecurityRestartValue.Text = FormatYesNo(_lastPcInformation?.RestartPending);
            SecurityStatusLabel.Text = string.Format(LocalizationService.Text("SecurityLoadedFormat"), _connectedTarget);
            await SafeLogAsync("Inventory", _connectedTarget, "Security inventory", "Success");
        }
        catch (Exception exception)
        {
            SecurityStatusLabel.Text = string.Format(LocalizationService.Text("SecurityErrorFormat"), exception.Message);
            await SafeLogAsync("Inventory", _connectedTarget, "Security inventory", "Error", exception.Message);
        }
        finally
        {
            RefreshSecurityButton.IsEnabled = true;
        }
    }

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
            await SafeLogAsync("Inventory", _connectedTarget, "Driver inventory", "Success", $"Drivers={Drivers.Count}");
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
            await SafeLogAsync("Inventory", _connectedTarget, "WinGet update check", result.Error is null ? "Success" : "Error",
                result.Error ?? $"Updates={Updates.Count}");
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
        ApplicationsPanel.Visibility = page == CentralPage.Applications ? Visibility.Visible : Visibility.Collapsed;
        SecurityPanel.Visibility = page == CentralPage.Security ? Visibility.Visible : Visibility.Collapsed;
        ActionLogPanel.Visibility = page == CentralPage.ActionLog ? Visibility.Visible : Visibility.Collapsed;
        HomeHelperPanel.Visibility = page == CentralPage.Home ? Visibility.Visible : Visibility.Collapsed;
        var deviceVisibility = page == CentralPage.Workstation && !string.IsNullOrWhiteSpace(_connectedTarget)
            ? Visibility.Visible
            : Visibility.Collapsed;
        DeviceHeaderPanel.Visibility = deviceVisibility;
        DeviceSummaryPanel.Visibility = deviceVisibility;
        DeviceDetailsPanel.Visibility = deviceVisibility;
        WelcomePanel.Visibility = page == CentralPage.Workstation && string.IsNullOrWhiteSpace(_connectedTarget)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void HomeHelperPanel_InventoryConnected(object? sender, HomeInventorySnapshot snapshot)
    {
        _connectedTarget = snapshot.DeviceName;
        _lastPcInformation = null;
        ConnectionTargetInput.Text = snapshot.DeviceName;
        DeviceTitleLabel.Text = snapshot.DeviceName.ToUpperInvariant();
        SelectedDeviceLabel.Text = $"  /  {snapshot.DeviceName}";
        ConnectionStatusLabel.Text = $"OPENFLEET HELPER · {LocalizationService.Text("Connected")}";
        WindowsVersionLabel.Text = snapshot.Windows.Caption.Replace("Microsoft ", string.Empty, StringComparison.OrdinalIgnoreCase);
        WindowsBuildLabel.Text = string.Format(LocalizationService.Text("BuildFormat"), snapshot.Windows.Version, snapshot.Windows.Build);
        var uptime = snapshot.Windows.LastBootUtc is { } boot ? DateTimeOffset.UtcNow - boot : TimeSpan.Zero;
        UptimeLabel.Text = string.Format(LocalizationService.Text("UptimeFormat"), Math.Max(0, uptime.Days), Math.Max(0, uptime.Hours));
        BootDateLabel.Text = snapshot.Windows.LastBootUtc is { } bootDate
            ? string.Format(LocalizationService.Text("BootDateFormat"), bootDate.LocalDateTime)
            : LocalizationService.Text("InformationUnavailable");
        FirewallStatusLabel.Text = FormatState(snapshot.Windows.FirewallEnabled);
        FirewallDetailsLabel.Text = LocalizationService.Text("HelperReadOnly");
        RestartStatusLabel.Text = FormatYesNo(snapshot.Windows.RestartPending);
        RestartReasonLabel.Text = snapshot.Windows.RestartPending == true
            ? LocalizationService.Text("SystemRegistry") : LocalizationService.Text("NoRestartPending");

        _applications.Clear();
        foreach (var software in snapshot.Software)
            _applications.Add(new ApplicationItem(software.Name, software.Publisher, software.Version, "Installed",
                "#2457D7A3", "#FF57D7A3", false, false, false, string.Empty));
        InventoryCountLabel.Text = string.Format(LocalizationService.Text("InventoryCountFormat"), _applications.Count);

        Drivers.Clear();
        foreach (var driver in snapshot.Drivers)
            Drivers.Add(new InstalledDriver(driver.DeviceName, "—", driver.Manufacturer, driver.DriverVersion,
                null, "—", false));
        DriversStatusLabel.Text = string.Format(LocalizationService.Text("DriversFoundFormat"), Drivers.Count, snapshot.DeviceName);
        ShowCentralPanel(CentralPage.Workstation);
        _ = SafeLogAsync("Home", snapshot.DeviceName, "Helper inventory", "Success",
            $"Software={snapshot.Software.Count}; Drivers={snapshot.Drivers.Count}");
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
            _lastPcInformation = information;
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
            else if (FleetPanel.Visibility != Visibility.Visible && SettingsPanel.Visibility != Visibility.Visible && UpdatesPanel.Visibility != Visibility.Visible && DriversPanel.Visibility != Visibility.Visible && ApplicationsPanel.Visibility != Visibility.Visible && SecurityPanel.Visibility != Visibility.Visible && ActionLogPanel.Visibility != Visibility.Visible)
                ShowCentralPanel(CentralPage.Workstation);

            await LoadSoftwareInventoryAsync(target);
            await SafeLogAsync("Connection", target, "Connect", "Success", information.Hostname);
        }
        catch (Exception exception)
        {
            ConnectionStatusLabel.Text = LocalizationService.Text("ConnectionFailedTitle").ToUpperInvariant();
            MessageBox.Show(
                string.Format(LocalizationService.Text("ConnectionFailedMessage"), target, exception.Message),
                LocalizationService.Text("ConnectionFailedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            await SafeLogAsync("Connection", target, "Connect", "Error", exception.Message);
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private void PopulateCurrentUser()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var account = identity.Name;
        var displayName = account.Contains('\\') ? account[(account.LastIndexOf('\\') + 1)..] : account;
        var isAdministrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        CurrentUserLabel.Text = displayName;
        CurrentUserLabel.ToolTip = account;
        UserInitialsLabel.Text = GetInitials(displayName);
        PrivilegeStatusLabel.Text = LocalizationService.Text(isAdministrator ? "ElevatedAdministrator" : "StandardUser");
        PrivilegeStatusLabel.Foreground = isAdministrator ? (Brush)FindResource("Success") : (Brush)FindResource("TextSecondary");
        ElevateButton.Visibility = isAdministrator ? Visibility.Collapsed : Visibility.Visible;

        var assembly = Assembly.GetExecutingAssembly();
        var version = (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? assembly.GetName().Version?.ToString() ?? "—").Split('+')[0];
        var build = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "—";
        AppVersionLabel.Text = string.Format(LocalizationService.Text("ProgramVersionBuildFormat"), version, build);
    }

    private static string GetInitials(string value)
    {
        var parts = value.Split(['.', '-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1
            ? string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])))
            : value[..Math.Min(2, value.Length)].ToUpperInvariant();
    }

    private void ElevateAccount_Click(object sender, RoutedEventArgs e)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return;
        if (MessageBox.Show(LocalizationService.Text("ElevateConfirmation"), LocalizationService.Text("ElevateAccount"),
                MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;

        try
        {
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas" });
            Close();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            // UAC was cancelled; the current non-elevated session remains open.
        }
        catch (Exception exception)
        {
            MessageBox.Show(string.Format(LocalizationService.Text("ElevationFailedFormat"), exception.Message),
                LocalizationService.Text("ElevationFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                    package.CanUninstall,
                    package.ProductCode));
            }

            if (IsLocalTarget(target))
            {
                var existingNames = _applications.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var package in await MsixInventoryService.GetAsync())
                {
                    if (!existingNames.Add(package.Name)) continue;
                    _applications.Add(new ApplicationItem(package.Name, package.Publisher, package.Version, "MsixInstalled",
                        "#244E9BFF", "#FF83B9FF", false, false, false, string.Empty));
                }
            }

            InventoryCountLabel.Text = string.Format(LocalizationService.Text("InventoryCountFormat"), _applications.Count);
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

    private static string FormatState(bool? value) => value switch
    {
        true => LocalizationService.Text("Enabled"),
        false => LocalizationService.Text("Disabled"),
        null => LocalizationService.Text("Unknown")
    };

    private static string FormatYesNo(bool? value) => value switch
    {
        true => LocalizationService.Text("Yes"),
        false => LocalizationService.Text("No"),
        null => LocalizationService.Text("Unknown")
    };

    private static async Task SafeLogAsync(string category, string target, string action, string result, string details = "")
    {
        try { await ActionLogService.AppendAsync(category, target, action, result, details); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

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
    Drivers,
    Applications,
    Security,
    ActionLog,
    Home
}

public sealed record ApplicationItem(string Name, string Publisher, string Version, string StatusResourceKey,
    string StatusBackgroundHex, string StatusForegroundHex, bool CanRepair, bool CanUpdate, bool CanUninstall,
    string ProductCode)
{
    public string Status => LocalizationService.Text(StatusResourceKey);
    public Brush StatusBackground => new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusBackgroundHex));
    public Brush StatusForeground => new SolidColorBrush((Color)ColorConverter.ConvertFromString(StatusForegroundHex));
}
