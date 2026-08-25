using Microsoft.Win32;
using System.IO;
using System.Management;
using System.Net;

namespace OpenFleetIT.App;

public sealed record PcInformation(
    string Hostname,
    string WindowsCaption,
    string WindowsVersion,
    string BuildNumber,
    DateTime LastBoot,
    bool? FirewallEnabled,
    int FirewallProfileCount,
    bool? RestartPending,
    string Manufacturer,
    string Model);

public static class PcInfoService
{
    public static Task<PcInformation> GetAsync(string target, CancellationToken cancellationToken = default) =>
        Task.Run(() => Get(target, cancellationToken), cancellationToken);

    private static PcInformation Get(string target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedTarget = NormalizeTarget(target);
        var isLocal = normalizedTarget == ".";
        var computerName = isLocal ? Environment.MachineName : normalizedTarget;
        var options = new ConnectionOptions
        {
            Authentication = AuthenticationLevel.PacketPrivacy,
            Impersonation = ImpersonationLevel.Impersonate,
            EnablePrivileges = true,
            Timeout = TimeSpan.FromSeconds(8)
        };

        var cimv2 = new ManagementScope($@"\\{normalizedTarget}\root\cimv2", options);
        cimv2.Connect();
        cancellationToken.ThrowIfCancellationRequested();

        string caption;
        string version;
        string build;
        DateTime lastBoot;
        string hostname;
        using (var searcher = new ManagementObjectSearcher(cimv2,
                   new ObjectQuery("SELECT Caption, Version, BuildNumber, LastBootUpTime, CSName FROM Win32_OperatingSystem")))
        using (var results = searcher.Get())
        {
            var os = results.Cast<ManagementObject>().FirstOrDefault()
                     ?? throw new InvalidOperationException("Windows did not return operating-system information.");
            caption = Value(os, "Caption");
            version = Value(os, "Version");
            build = Value(os, "BuildNumber");
            hostname = Value(os, "CSName", computerName);
            lastBoot = ManagementDateTimeConverter.ToDateTime(Value(os, "LastBootUpTime"));
        }

        string manufacturer = "—";
        string model = "—";
        using (var searcher = new ManagementObjectSearcher(cimv2,
                   new ObjectQuery("SELECT Manufacturer, Model FROM Win32_ComputerSystem")))
        using (var results = searcher.Get())
        {
            var system = results.Cast<ManagementObject>().FirstOrDefault();
            if (system is not null)
            {
                manufacturer = Value(system, "Manufacturer", "—");
                model = Value(system, "Model", "—");
            }
        }

        var (firewallEnabled, profileCount) = ReadFirewall(normalizedTarget, options);
        var restartPending = ReadRestartPending(computerName, isLocal);
        return new PcInformation(hostname, caption, version, build, lastBoot, firewallEnabled, profileCount,
            restartPending, manufacturer, model);
    }

    private static (bool? Enabled, int ProfileCount) ReadFirewall(string target, ConnectionOptions options)
    {
        try
        {
            var scope = new ManagementScope($@"\\{target}\root\StandardCimv2", options);
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT Enabled FROM MSFT_NetFirewallProfile"));
            using var results = searcher.Get();
            var profiles = results.Cast<ManagementObject>().ToList();
            return (profiles.Count == 0 ? null : profiles.All(item => Convert.ToBoolean(item["Enabled"])), profiles.Count);
        }
        catch (ManagementException)
        {
            return (null, 0);
        }
        catch (UnauthorizedAccessException)
        {
            return (null, 0);
        }
    }

    private static bool? ReadRestartPending(string computerName, bool isLocal)
    {
        try
        {
            using var baseKey = isLocal
                ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                : RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, computerName, RegistryView.Registry64);
            using var cbs = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            using var wu = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            using var sessionManager = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            return cbs is not null || wu is not null || sessionManager?.GetValue("PendingFileRenameOperations") is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static string NormalizeTarget(string target)
    {
        var value = target.Trim();
        if (value.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || value.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            || value is "127.0.0.1" or "::1") return ".";

        if (IPAddress.TryParse(value, out _)) return value;
        return value.TrimEnd('.');
    }

    private static string Value(ManagementBaseObject item, string property, string fallback = "") =>
        item[property]?.ToString()?.Trim() is { Length: > 0 } value ? value : fallback;
}
