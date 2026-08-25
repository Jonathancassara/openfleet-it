using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;

namespace OpenFleetIT.App;

public partial class FleetScanWindow : UserControl
{
    private readonly ObservableCollection<ScanResult> _results = [];

    public FleetScanWindow()
    {
        InitializeComponent();
        ResultsGrid.ItemsSource = _results;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateRange(StartIpInput.Text, EndIpInput.Text, out var addresses, out var error))
        {
            MessageBox.Show(error, LocalizationService.Text("InvalidRangeTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _results.Clear();
        ScanButton.IsEnabled = false;
        ScanProgress.Value = 0;
        ScanStatus.Text = string.Format(LocalizationService.Text("ScanningAddresses"), addresses.Count);

        var completed = 0;
        var liveResults = new ConcurrentBag<ScanResult>();
        using var gate = new SemaphoreSlim(24);
        var tasks = addresses.Select(async address =>
        {
            await gate.WaitAsync();
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, TimeSpan.FromMilliseconds(700));
                if (reply.Status == IPStatus.Success)
                {
                    var hostname = await ResolveHostnameAsync(address);
                    liveResults.Add(new ScanResult(address.ToString(), hostname, LocalizationService.Text("OnlineStatus"), $"{reply.RoundtripTime} ms"));
                }
            }
            catch (PingException)
            {
                // An unreachable or filtered host is an expected scan result.
            }
            finally
            {
                gate.Release();
                var current = Interlocked.Increment(ref completed);
                await Dispatcher.InvokeAsync(() => ScanProgress.Value = current * 100d / addresses.Count);
            }
        });

        await Task.WhenAll(tasks);
        foreach (var result in liveResults.OrderBy(item => ParseIpv4(item.Address))) _results.Add(result);
        ScanStatus.Text = string.Format(LocalizationService.Text("ScanComplete"), _results.Count, addresses.Count);
        await ActionLogService.AppendAsync("Discovery", $"{StartIpInput.Text.Trim()}-{EndIpInput.Text.Trim()}",
            "IPv4 scan", "Success", $"{_results.Count}/{addresses.Count} active");
        ScanButton.IsEnabled = true;
    }

    private static bool TryCreateRange(string startText, string endText, out List<IPAddress> addresses, out string error)
    {
        addresses = [];
        error = string.Empty;
        if (!IPAddress.TryParse(startText.Trim(), out var start) || start.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
            || !IPAddress.TryParse(endText.Trim(), out var end) || end.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            error = LocalizationService.Text("InvalidIpv4");
            return false;
        }

        var startValue = ParseIpv4(start.ToString());
        var endValue = ParseIpv4(end.ToString());
        if (endValue < startValue)
        {
            error = LocalizationService.Text("EndBeforeStart");
            return false;
        }

        if (endValue - startValue + 1 > 256)
        {
            error = LocalizationService.Text("RangeTooLarge");
            return false;
        }

        for (var value = startValue; value <= endValue; value++)
            addresses.Add(new IPAddress([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]));
        return true;
    }

    private static uint ParseIpv4(string address)
    {
        var bytes = IPAddress.Parse(address).GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static async Task<string> ResolveHostnameAsync(IPAddress address)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(address).WaitAsync(TimeSpan.FromMilliseconds(900));
            return string.IsNullOrWhiteSpace(entry.HostName) ? "—" : entry.HostName;
        }
        catch (Exception exception) when (exception is System.Net.Sockets.SocketException or TimeoutException)
        {
            return "—";
        }
    }

}

public sealed record ScanResult(string Address, string Hostname, string Status, string Latency);
