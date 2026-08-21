using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OpenFleetIT.App;

public partial class FleetScanWindow : Window
{
    private readonly ObservableCollection<ScanResult> _results = [];

    public FleetScanWindow()
    {
        InitializeComponent();
        ResultsGrid.ItemsSource = _results;
        SourceInitialized += (_, _) => EnableDarkTitleBar();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateRange(StartIpInput.Text, EndIpInput.Text, out var addresses, out var error))
        {
            MessageBox.Show(error, "Plage IP invalide", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _results.Clear();
        ScanButton.IsEnabled = false;
        ScanProgress.Value = 0;
        ScanStatus.Text = $"Analyse de {addresses.Count} adresses…";

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
                    liveResults.Add(new ScanResult(address.ToString(), hostname, "En ligne", $"{reply.RoundtripTime} ms"));
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
        ScanStatus.Text = $"{_results.Count} poste(s) actif(s) sur {addresses.Count}";
        ScanButton.IsEnabled = true;
    }

    private static bool TryCreateRange(string startText, string endText, out List<IPAddress> addresses, out string error)
    {
        addresses = [];
        error = string.Empty;
        if (!IPAddress.TryParse(startText.Trim(), out var start) || start.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
            || !IPAddress.TryParse(endText.Trim(), out var end) || end.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            error = "Saisissez deux adresses IPv4 valides.";
            return false;
        }

        var startValue = ParseIpv4(start.ToString());
        var endValue = ParseIpv4(end.ToString());
        if (endValue < startValue)
        {
            error = "L’adresse de fin doit être supérieure ou égale à l’adresse de début.";
            return false;
        }

        if (endValue - startValue + 1 > 256)
        {
            error = "La plage est limitée à 256 adresses par scan.";
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

    private void EnableDarkTitleBar()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        const int darkModeAttribute = 20;
        var enabled = 1;
        _ = DwmSetWindowAttribute(hwnd, darkModeAttribute, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}

public sealed record ScanResult(string Address, string Hostname, string Status, string Latency);
