using System.Security.Cryptography;

namespace OpenFleetIT.Core;

public static class HomeProtocol
{
    public const string Version = "1";
    public const int DefaultPort = 47831;
    public const int MaxPairingCodeAttempts = 5;
    public static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromMinutes(5);
}

public sealed record HelperHealth(string ProtocolVersion, string HelperVersion, string DeviceName,
    DateTimeOffset ServerTimeUtc, IReadOnlyList<string> Capabilities);

public sealed record LocalPairingInfo(string DeviceName, string PairingCode, string ServerCertificateSha256,
    DateTimeOffset ExpiresAtUtc);

public sealed record PairingRequest(string Code, string ControllerName);

public sealed record PairingResponse(string DeviceId, string DeviceName, string ClientCertificatePfx,
    string ClientCertificatePassword, string ServerCertificateSha256, DateTimeOffset ExpiresAtUtc);

public sealed record HomeInventorySnapshot(string ProtocolVersion, string DeviceId, string DeviceName,
    DateTimeOffset CollectedAtUtc, WindowsSnapshot Windows, IReadOnlyList<SoftwareSnapshot> Software,
    IReadOnlyList<DriverSnapshot> Drivers);

public sealed record WindowsSnapshot(string Caption, string Version, string Build, DateTimeOffset? LastBootUtc,
    bool? FirewallEnabled, bool? RestartPending);

public sealed record SoftwareSnapshot(string Name, string Publisher, string Version);
public sealed record DriverSnapshot(string DeviceName, string Manufacturer, string DriverVersion);

public static class PairingCode
{
    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        return (BitConverter.ToUInt32(bytes) % 1_000_000).ToString("D6");
    }

    public static bool IsValidFormat(string? value) =>
        value is { Length: 6 } && value.All(char.IsAsciiDigit);

    public static bool FixedTimeEquals(string expected, string supplied)
    {
        var left = System.Text.Encoding.UTF8.GetBytes(expected);
        var right = System.Text.Encoding.UTF8.GetBytes(supplied ?? string.Empty);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
