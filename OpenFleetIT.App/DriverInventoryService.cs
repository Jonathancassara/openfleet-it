using System.Management;

namespace OpenFleetIT.App;

public static class DriverInventoryService
{
    public static Task<IReadOnlyList<InstalledDriver>> GetAsync(string target, CancellationToken cancellationToken = default) =>
        Task.Run(() => Get(target, cancellationToken), cancellationToken);

    private static IReadOnlyList<InstalledDriver> Get(string target, CancellationToken cancellationToken)
    {
        var normalizedTarget = NormalizeTarget(target);
        var options = new ConnectionOptions
        {
            Authentication = AuthenticationLevel.PacketPrivacy,
            Impersonation = ImpersonationLevel.Impersonate,
            EnablePrivileges = true,
            Timeout = TimeSpan.FromSeconds(12)
        };
        var scope = new ManagementScope($@"\\{normalizedTarget}\root\cimv2", options);
        scope.Connect();

        using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(
            "SELECT DeviceName, DeviceClass, Manufacturer, DriverVersion, DriverDate, InfName, IsSigned FROM Win32_PnPSignedDriver"));
        using var results = searcher.Get();
        var drivers = new List<InstalledDriver>();

        foreach (var item in results.Cast<ManagementObject>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Read(item, "DeviceName");
            if (string.IsNullOrWhiteSpace(name)) continue;

            drivers.Add(new InstalledDriver(
                name,
                Read(item, "DeviceClass", "—"),
                Read(item, "Manufacturer", "—"),
                Read(item, "DriverVersion", "—"),
                ReadDriverDate(item),
                Read(item, "InfName", "—"),
                item["IsSigned"] is not null && Convert.ToBoolean(item["IsSigned"])));
        }

        return drivers
            .GroupBy(driver => $"{driver.Name}\u001f{driver.InfName}\u001f{driver.Version}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(driver => driver.DeviceClass, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(driver => driver.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static DateTime? ReadDriverDate(ManagementBaseObject item)
    {
        var value = Read(item, "DriverDate");
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return ManagementDateTimeConverter.ToDateTime(value); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static string NormalizeTarget(string target)
    {
        var value = target.Trim();
        return value.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || value.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
               || value is "127.0.0.1" or "::1" ? "." : value.TrimEnd('.');
    }

    private static string Read(ManagementBaseObject item, string property, string fallback = "") =>
        item[property]?.ToString()?.Trim() is { Length: > 0 } value ? value : fallback;
}

public sealed record InstalledDriver(string Name, string DeviceClass, string Manufacturer, string Version,
    DateTime? DriverDate, string InfName, bool IsSigned)
{
    public string DriverDateDisplay => DriverDate?.ToString("d") ?? "—";
    public string SignatureResourceKey => IsSigned ? "Signed" : "Unsigned";
    public string Signature => LocalizationService.Text(SignatureResourceKey);
}
