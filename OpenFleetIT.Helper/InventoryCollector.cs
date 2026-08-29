using Microsoft.Win32;
using OpenFleetIT.Core;
using System.Management;

namespace OpenFleetIT.Helper;

internal static class InventoryCollector
{
    public static HomeInventorySnapshot Collect(string deviceId) => new(HomeProtocol.Version, deviceId,
        Environment.MachineName, DateTimeOffset.UtcNow, ReadWindows(), ReadSoftware(), ReadDrivers());

    private static WindowsSnapshot ReadWindows()
    {
        using var searcher = new ManagementObjectSearcher("SELECT Caption,Version,BuildNumber,LastBootUpTime FROM Win32_OperatingSystem");
        using var results = searcher.Get();
        var item = results.Cast<ManagementObject>().FirstOrDefault();
        DateTimeOffset? boot = null;
        var rawBoot = item?["LastBootUpTime"]?.ToString();
        if (!string.IsNullOrWhiteSpace(rawBoot)) boot = ManagementDateTimeConverter.ToDateTime(rawBoot).ToUniversalTime();
        return new WindowsSnapshot(item?["Caption"]?.ToString() ?? "Windows", item?["Version"]?.ToString() ?? "",
            item?["BuildNumber"]?.ToString() ?? "", boot, ReadFirewall(), ReadRestartPending());
    }

    private static IReadOnlyList<SoftwareSnapshot> ReadSoftware()
    {
        var items = new Dictionary<string, SoftwareSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
        using (var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
        {
            if (uninstall is null) continue;
            foreach (var name in uninstall.GetSubKeyNames())
            using (var key = uninstall.OpenSubKey(name))
            {
                var displayName = key?.GetValue("DisplayName")?.ToString();
                if (string.IsNullOrWhiteSpace(displayName)) continue;
                var item = new SoftwareSnapshot(displayName, key?.GetValue("Publisher")?.ToString() ?? "",
                    key?.GetValue("DisplayVersion")?.ToString() ?? "");
                items[$"{item.Name}\0{item.Version}"] = item;
            }
        }
        return items.Values.OrderBy(item => item.Name).ToArray();
    }

    private static IReadOnlyList<DriverSnapshot> ReadDrivers()
    {
        using var searcher = new ManagementObjectSearcher("SELECT DeviceName,Manufacturer,DriverVersion FROM Win32_PnPSignedDriver");
        using var results = searcher.Get();
        return results.Cast<ManagementObject>()
            .Select(item => new DriverSnapshot(item["DeviceName"]?.ToString() ?? "Unknown",
                item["Manufacturer"]?.ToString() ?? "", item["DriverVersion"]?.ToString() ?? ""))
            .OrderBy(item => item.DeviceName).ToArray();
    }

    private static bool? ReadFirewall()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
            return key?.GetValue("EnableFirewall") is int enabled ? enabled != 0 : null;
        }
        catch { return null; }
    }

    private static bool? ReadRestartPending()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            return key is not null;
        }
        catch { return null; }
    }
}
