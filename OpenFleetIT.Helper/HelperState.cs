using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using OpenFleetIT.Core;

namespace OpenFleetIT.Helper;

public sealed class HelperState
{
    private readonly object _gate = new();
    private readonly string _stateDirectory;
    private readonly HashSet<string> _pairedThumbprints;
    private int _failedAttempts;
    private bool _pairingUsed;

    private HelperState(string stateDirectory, string deviceId, X509Certificate2 ca, X509Certificate2 server,
        HashSet<string> pairedThumbprints)
    {
        _stateDirectory = stateDirectory;
        DeviceId = deviceId;
        AuthorityCertificate = ca;
        ServerCertificate = server;
        _pairedThumbprints = pairedThumbprints;
        PairingCode = OpenFleetIT.Core.PairingCode.Create();
        PairingExpiresAtUtc = DateTimeOffset.UtcNow.Add(HomeProtocol.PairingCodeLifetime);
    }

    public string DeviceId { get; }
    public X509Certificate2 AuthorityCertificate { get; }
    public X509Certificate2 ServerCertificate { get; }
    public string PairingCode { get; }
    public DateTimeOffset PairingExpiresAtUtc { get; }
    public string HelperVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0";
    public string ServerFingerprint => Convert.ToHexString(SHA256.HashData(ServerCertificate.RawData));

    public static HelperState LoadOrCreate()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenFleetIT", "Helper");
        Directory.CreateDirectory(directory);
        var devicePath = Path.Combine(directory, "device-id.txt");
        var deviceId = File.Exists(devicePath) ? File.ReadAllText(devicePath).Trim() : Guid.NewGuid().ToString("D");
        if (!File.Exists(devicePath)) File.WriteAllText(devicePath, deviceId);

        var ca = CertificateFactory.LoadOrCreateAuthority(deviceId);
        var server = CertificateFactory.LoadOrCreateServer(ca, deviceId);
        var pairedPath = Path.Combine(directory, "paired-controllers.json");
        var paired = File.Exists(pairedPath)
            ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(pairedPath)) ?? []
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return new HelperState(directory, deviceId, ca, server, paired);
    }

    public HelperHealth Health() => new(HomeProtocol.Version, HelperVersion, Environment.MachineName,
        DateTimeOffset.UtcNow, ["inventory.windows", "inventory.software", "inventory.drivers"]);

    public IResult Pair(PairingRequest request)
    {
        lock (_gate)
        {
            if (_pairingUsed || DateTimeOffset.UtcNow > PairingExpiresAtUtc || _failedAttempts >= HomeProtocol.MaxPairingCodeAttempts)
                return Results.StatusCode(StatusCodes.Status410Gone);
            if (!OpenFleetIT.Core.PairingCode.IsValidFormat(request.Code) ||
                !OpenFleetIT.Core.PairingCode.FixedTimeEquals(PairingCode, request.Code))
            {
                _failedAttempts++;
                return Results.Unauthorized();
            }

            var controllerName = CertificateFactory.SafeCommonName(request.ControllerName);
            var issued = CertificateFactory.IssueClient(AuthorityCertificate, controllerName);
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var pfx = issued.Export(X509ContentType.Pfx, password);
            _pairedThumbprints.Add(issued.Thumbprint);
            PersistPairedControllers();
            _pairingUsed = true;
            return Results.Ok(new PairingResponse(DeviceId, Environment.MachineName, Convert.ToBase64String(pfx), password,
                ServerFingerprint, issued.NotAfter.ToUniversalTime()));
        }
    }

    public bool IsPaired(X509Certificate2 certificate) => _pairedThumbprints.Contains(certificate.Thumbprint);

    private void PersistPairedControllers() => File.WriteAllText(Path.Combine(_stateDirectory, "paired-controllers.json"),
        JsonSerializer.Serialize(_pairedThumbprints.OrderBy(value => value)));
}
