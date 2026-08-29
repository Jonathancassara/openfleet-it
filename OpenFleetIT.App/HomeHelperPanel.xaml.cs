using OpenFleetIT.Core;
using System.IO;
using System.Net.Http;
using System.Security.Authentication;
using System.Windows;
using System.Windows.Controls;

namespace OpenFleetIT.App;

public partial class HomeHelperPanel : UserControl
{
    public event EventHandler<HomeInventorySnapshot>? InventoryConnected;

    public HomeHelperPanel() => InitializeComponent();

    private async void Verify_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var health = await HomeHelperClient.GetHealthAsync(HostInput.Text, FingerprintInput.Text);
        StatusLabel.Text = string.Format(LocalizationService.Text("HelperVerifiedFormat"), health.DeviceName, health.HelperVersion);
    });

    private async void Pair_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var record = await HomeHelperClient.PairAsync(HostInput.Text, FingerprintInput.Text, CodeInput.Text,
            $"OpenFleet on {Environment.MachineName}");
        CodeInput.Clear();
        StatusLabel.Text = string.Format(LocalizationService.Text("PairingSucceededFormat"), record.DeviceName);
    });

    private async void Connect_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var snapshot = await HomeHelperClient.GetInventoryAsync(HostInput.Text);
        StatusLabel.Text = string.Format(LocalizationService.Text("HomeInventoryLoadedFormat"), snapshot.DeviceName,
            snapshot.Software.Count, snapshot.Drivers.Count);
        InventoryConnected?.Invoke(this, snapshot);
    });

    private async Task RunAsync(Func<Task> action)
    {
        VerifyButton.IsEnabled = PairButton.IsEnabled = ConnectButton.IsEnabled = false;
        StatusLabel.Text = LocalizationService.Text("Working");
        try { await action(); }
        catch (Exception exception) when (exception is ArgumentException or HttpRequestException or InvalidOperationException
                                           or AuthenticationException or IOException or FormatException)
        {
            StatusLabel.Text = string.Format(LocalizationService.Text("HomeConnectionErrorFormat"), exception.Message);
        }
        finally { VerifyButton.IsEnabled = PairButton.IsEnabled = ConnectButton.IsEnabled = true; }
    }
}
