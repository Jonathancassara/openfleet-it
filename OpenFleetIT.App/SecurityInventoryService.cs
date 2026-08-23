using Microsoft.Win32;
using System.Management;
using System.IO;

namespace OpenFleetIT.App;

public static class SecurityInventoryService
{
    public static Task<SecuritySnapshot> GetAsync(string target, CancellationToken cancellationToken = default) =>
        Task.Run(() => Get(target, cancellationToken), cancellationToken);

    private static SecuritySnapshot Get(string target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeTarget(target);
        var options = new ConnectionOptions
        {
            Authentication = AuthenticationLevel.PacketPrivacy,
            Impersonation = ImpersonationLevel.Impersonate,
            EnablePrivileges = true,
            Timeout = TimeSpan.FromSeconds(10)
        };

        var defender = ReadDefender(normalized, options);
        var bitLocker = ReadBitLocker(normalized, options);
        var tpm = ReadTpm(normalized, options);
        var secureBoot = ReadSecureBoot(normalized, normalized == ".");
        return new SecuritySnapshot(defender.Enabled, defender.RealtimeProtection, defender.SignatureAgeDays,
            bitLocker, tpm.Present, tpm.Enabled, secureBoot);
    }

    private static (bool? Enabled, bool? RealtimeProtection, int? SignatureAgeDays) ReadDefender(string target, ConnectionOptions options)
    {
        try
        {
            var scope = new ManagementScope($@"\\{target}\root\Microsoft\Windows\Defender", options);
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT AMServiceEnabled, RealTimeProtectionEnabled, AntivirusSignatureAge FROM MSFT_MpComputerStatus"));
            using var results = searcher.Get();
            var item = results.Cast<ManagementObject>().FirstOrDefault();
            return item is null ? (null, null, null) :
                (ToNullableBool(item["AMServiceEnabled"]), ToNullableBool(item["RealTimeProtectionEnabled"]), ToNullableInt(item["AntivirusSignatureAge"]));
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException)
        {
            return (null, null, null);
        }
    }

    private static bool? ReadBitLocker(string target, ConnectionOptions options)
    {
        try
        {
            var scope = new ManagementScope($@"\\{target}\root\cimv2\Security\MicrosoftVolumeEncryption", options);
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT ProtectionStatus, DriveLetter FROM Win32_EncryptableVolume"));
            using var results = searcher.Get();
            var volumes = results.Cast<ManagementObject>().Where(item => !string.IsNullOrWhiteSpace(item["DriveLetter"]?.ToString())).ToList();
            return volumes.Count == 0 ? null : volumes.All(item => Convert.ToUInt32(item["ProtectionStatus"]) == 1);
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static (bool? Present, bool? Enabled) ReadTpm(string target, ConnectionOptions options)
    {
        try
        {
            var scope = new ManagementScope($@"\\{target}\root\cimv2\Security\MicrosoftTpm", options);
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT IsEnabled_InitialValue FROM Win32_Tpm"));
            using var results = searcher.Get();
            var item = results.Cast<ManagementObject>().FirstOrDefault();
            return item is null ? (false, false) : (true, ToNullableBool(item["IsEnabled_InitialValue"]));
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException)
        {
            return (null, null);
        }
    }

    private static bool? ReadSecureBoot(string target, bool local)
    {
        try
        {
            using var baseKey = local
                ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                : RegistryKey.OpenRemoteBaseKey(RegistryHive.LocalMachine, target, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            return key?.GetValue("UEFISecureBootEnabled") is int value ? value == 1 : null;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return null;
        }
    }

    private static string NormalizeTarget(string target) => target.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || target.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
        || target is "127.0.0.1" or "::1" or "." ? "." : target.TrimEnd('.');

    private static bool? ToNullableBool(object? value) => value is null ? null : Convert.ToBoolean(value);
    private static int? ToNullableInt(object? value) => value is null ? null : Convert.ToInt32(value);
}

public sealed record SecuritySnapshot(bool? DefenderEnabled, bool? RealtimeProtectionEnabled, int? SignatureAgeDays,
    bool? BitLockerProtected, bool? TpmPresent, bool? TpmEnabled, bool? SecureBootEnabled);
