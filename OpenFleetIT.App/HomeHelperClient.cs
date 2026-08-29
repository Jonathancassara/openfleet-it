using OpenFleetIT.Core;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Text.Json;

namespace OpenFleetIT.App;

public static class HomeHelperClient
{
    private static readonly string PairingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenFleetIT", "home-pairings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<HelperHealth> GetHealthAsync(string host, string fingerprint,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(host, fingerprint, null);
        return await client.GetFromJsonAsync<HelperHealth>("health", cancellationToken)
               ?? throw new InvalidDataException("The Helper returned an empty health response.");
    }

    public static async Task<HomePairingRecord> PairAsync(string host, string fingerprint, string code,
        string controllerName, CancellationToken cancellationToken = default)
    {
        if (!PairingCode.IsValidFormat(code)) throw new ArgumentException("The pairing code must contain six digits.", nameof(code));
        var normalizedFingerprint = NormalizeFingerprint(fingerprint);
        using var client = CreateClient(host, normalizedFingerprint, null);
        using var response = await client.PostAsJsonAsync("v1/pair", new PairingRequest(code, controllerName), cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Pairing failed with HTTP {(int)response.StatusCode}.", null, response.StatusCode);
        var pairing = await response.Content.ReadFromJsonAsync<PairingResponse>(cancellationToken)
                      ?? throw new InvalidDataException("The Helper returned an empty pairing response.");
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(normalizedFingerprint), Convert.FromHexString(NormalizeFingerprint(pairing.ServerCertificateSha256))))
            throw new AuthenticationException("The Helper fingerprint changed during pairing.");

        var pfx = Convert.FromBase64String(pairing.ClientCertificatePfx);
        X509Certificate2? certificate = null;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12(pfx, pairing.ClientCertificatePassword,
                X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
            var record = new HomePairingRecord(host.Trim(), pairing.DeviceId, pairing.DeviceName,
                normalizedFingerprint, certificate.Thumbprint, DateTimeOffset.UtcNow);
            try
            {
                await SavePairingAsync(record, cancellationToken);
                return record;
            }
            catch
            {
                store.Remove(certificate);
                throw;
            }
        }
        finally
        {
            certificate?.Dispose();
            CryptographicOperations.ZeroMemory(pfx);
        }
    }

    public static async Task<HomeInventorySnapshot> GetInventoryAsync(string host,
        CancellationToken cancellationToken = default)
    {
        var pairing = await FindPairingAsync(host, cancellationToken)
                      ?? throw new InvalidOperationException("This device is not paired yet.");
        using var certificate = FindCertificate(pairing.ClientCertificateThumbprint)
                                ?? throw new InvalidOperationException("The paired client certificate is missing from the Windows certificate store.");
        using var client = CreateClient(host, pairing.ServerFingerprintSha256, certificate);
        return await client.GetFromJsonAsync<HomeInventorySnapshot>("v1/inventory", cancellationToken)
               ?? throw new InvalidDataException("The Helper returned an empty inventory response.");
    }

    public static async Task<HomePairingRecord?> FindPairingAsync(string host,
        CancellationToken cancellationToken = default)
    {
        var records = await LoadPairingsAsync(cancellationToken);
        return records.FirstOrDefault(record => record.Host.Equals(host.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeHost(string host)
    {
        var value = host.Trim();
        if (value.Length is < 1 or > 255 || value.Any(char.IsWhiteSpace) || value.Contains('/') || value.Contains('\\'))
            throw new ArgumentException("Enter a valid hostname or IP address.", nameof(host));
        return value;
    }

    public static string NormalizeFingerprint(string fingerprint)
    {
        var value = new string((fingerprint ?? string.Empty).Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        if (value.Length != 64) throw new ArgumentException("The SHA-256 fingerprint must contain 64 hexadecimal characters.", nameof(fingerprint));
        return value;
    }

    private static HttpClient CreateClient(string host, string fingerprint, X509Certificate2? certificate)
    {
        var expected = Convert.FromHexString(NormalizeFingerprint(fingerprint));
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, server, _, _) =>
        {
            if (server is null) return false;
            var actual = SHA256.HashData(server.RawData);
            return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
        };
        if (certificate is not null) handler.ClientCertificates.Add(certificate);
        return new HttpClient(handler)
        {
            BaseAddress = new UriBuilder(Uri.UriSchemeHttps, NormalizeHost(host), HomeProtocol.DefaultPort).Uri,
            Timeout = TimeSpan.FromSeconds(30),
            MaxResponseContentBufferSize = 16 * 1024 * 1024
        };
    }

    private static X509Certificate2? FindCertificate(string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        return store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
            .OfType<X509Certificate2>().FirstOrDefault(certificate => certificate.HasPrivateKey);
    }

    private static async Task<IReadOnlyList<HomePairingRecord>> LoadPairingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(PairingsPath)) return [];
            await using var stream = File.OpenRead(PairingsPath);
            return await JsonSerializer.DeserializeAsync<List<HomePairingRecord>>(stream, cancellationToken: cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is IOException or JsonException) { return []; }
    }

    private static async Task SavePairingAsync(HomePairingRecord record, CancellationToken cancellationToken)
    {
        var records = (await LoadPairingsAsync(cancellationToken)).ToList();
        records.RemoveAll(item => item.Host.Equals(record.Host, StringComparison.OrdinalIgnoreCase)
                                  || item.DeviceId.Equals(record.DeviceId, StringComparison.OrdinalIgnoreCase));
        records.Add(record);
        Directory.CreateDirectory(Path.GetDirectoryName(PairingsPath)!);
        await using var stream = File.Create(PairingsPath);
        await JsonSerializer.SerializeAsync(stream, records, JsonOptions, cancellationToken);
    }
}

public sealed record HomePairingRecord(string Host, string DeviceId, string DeviceName,
    string ServerFingerprintSha256, string ClientCertificateThumbprint, DateTimeOffset PairedAtUtc);
