using Microsoft.Win32;
using System.IO;
using System.Security;

namespace OpenFleetIT.App;

public static class SoftwareInventoryService
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static Task<IReadOnlyList<SoftwarePackage>> GetAsync(string target, CancellationToken cancellationToken = default) =>
        Task.Run(() => Get(target, cancellationToken), cancellationToken);

    private static IReadOnlyList<SoftwarePackage> Get(string target, CancellationToken cancellationToken)
    {
        var packages = new List<SoftwarePackage>();
        var failures = new List<Exception>();
        var local = IsLocalTarget(target);

        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, target, local, packages, failures, cancellationToken);
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry32, target, local, packages, failures, cancellationToken);

        if (local)
        {
            ReadHive(RegistryHive.CurrentUser, RegistryView.Registry64, target, true, packages, failures, cancellationToken);
            ReadHive(RegistryHive.CurrentUser, RegistryView.Registry32, target, true, packages, failures, cancellationToken);
        }

        if (packages.Count == 0 && failures.Count > 0)
            throw new IOException("The Windows software inventory could not be read.", failures[0]);

        return packages
            .GroupBy(package => $"{package.Name}\u001f{package.Version}\u001f{package.Publisher}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(package => package.CanRepair).ThenByDescending(package => package.CanUninstall).First())
            .OrderBy(package => package.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static void ReadHive(RegistryHive hive, RegistryView view, string target, bool local,
        ICollection<SoftwarePackage> packages, ICollection<Exception> failures, CancellationToken cancellationToken)
    {
        try
        {
            using var baseKey = local
                ? RegistryKey.OpenBaseKey(hive, view)
                : RegistryKey.OpenRemoteBaseKey(hive, target, view);
            using var uninstallKey = baseKey.OpenSubKey(UninstallPath, writable: false);
            if (uninstallKey is null) return;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var appKey = uninstallKey.OpenSubKey(subKeyName, writable: false);
                    if (appKey is null || ReadInt(appKey, "SystemComponent") == 1) continue;

                    var name = ReadString(appKey, "DisplayName");
                    if (string.IsNullOrWhiteSpace(name) || IsUpdateEntry(appKey)) continue;

                    var uninstall = ReadString(appKey, "UninstallString");
                    var quietUninstall = ReadString(appKey, "QuietUninstallString");
                    var modifyPath = ReadString(appKey, "ModifyPath");
                    var isMsi = ReadInt(appKey, "WindowsInstaller") == 1 && Guid.TryParse(subKeyName, out _);

                    packages.Add(new SoftwarePackage(
                        name.Trim(),
                        ReadString(appKey, "Publisher").Trim(),
                        ReadString(appKey, "DisplayVersion").Trim(),
                        ReadString(appKey, "InstallDate").Trim(),
                        view == RegistryView.Registry64 ? "64-bit" : "32-bit",
                        hive == RegistryHive.CurrentUser ? "Current user" : "All users",
                        !string.IsNullOrWhiteSpace(modifyPath) || isMsi,
                        !string.IsNullOrWhiteSpace(quietUninstall) || !string.IsNullOrWhiteSpace(uninstall),
                        modifyPath,
                        quietUninstall,
                        uninstall));
                }
                catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
                {
                    failures.Add(exception);
                }
            }
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
        {
            failures.Add(exception);
        }
    }

    private static bool IsUpdateEntry(RegistryKey key)
    {
        var releaseType = ReadString(key, "ReleaseType");
        return !string.IsNullOrWhiteSpace(ReadString(key, "ParentKeyName"))
               || releaseType.Contains("Update", StringComparison.OrdinalIgnoreCase)
               || releaseType.Contains("Hotfix", StringComparison.OrdinalIgnoreCase)
               || releaseType.Contains("Security", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalTarget(string target) =>
        string.Equals(target, "localhost", StringComparison.OrdinalIgnoreCase)
        || target is "127.0.0.1" or "::1" or "."
        || string.Equals(target.TrimEnd('.'), Environment.MachineName, StringComparison.OrdinalIgnoreCase);

    private static string ReadString(RegistryKey key, string name) => key.GetValue(name)?.ToString() ?? string.Empty;

    private static int ReadInt(RegistryKey key, string name) =>
        key.GetValue(name) switch
        {
            int value => value,
            long value => checked((int)value),
            string value when int.TryParse(value, out var parsed) => parsed,
            _ => 0
        };
}

public sealed record SoftwarePackage(
    string Name,
    string Publisher,
    string Version,
    string InstallDate,
    string Architecture,
    string Scope,
    bool CanRepair,
    bool CanUninstall,
    string ModifyCommand,
    string QuietUninstallCommand,
    string UninstallCommand);
