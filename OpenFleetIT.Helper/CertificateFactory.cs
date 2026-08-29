using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OpenFleetIT.Helper;

internal static class CertificateFactory
{
    public static X509Certificate2 LoadOrCreateAuthority(string deviceId)
    {
        var subject = $"CN=OpenFleet Helper CA {deviceId}";
        var existing = FindInUserStore(subject);
        if (existing is not null) return existing;
        using var key = RSA.Create(3072);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(5));
        return PersistInUserStore(generated);
    }

    public static X509Certificate2 LoadOrCreateServer(X509Certificate2 authority, string deviceId)
    {
        var subject = $"CN=OpenFleet Helper Server {deviceId}";
        var existing = FindInUserStore(subject);
        if (existing is not null) return existing;
        using var key = RSA.Create(3072);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new("1.3.6.1.5.5.7.3.1")], true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(Environment.MachineName);
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        san.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(san.Build());
        var serial = RandomNumberGenerator.GetBytes(16);
        using var issued = request.Create(authority, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2), serial);
        using var withKey = issued.CopyWithPrivateKey(key);
        return PersistInUserStore(withKey);
    }

    public static X509Certificate2 IssueClient(X509Certificate2 authority, string controllerName)
    {
        using var key = RSA.Create(3072);
        var request = new CertificateRequest($"CN={controllerName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new("1.3.6.1.5.5.7.3.2")], true));
        using var issued = request.Create(authority, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1),
            RandomNumberGenerator.GetBytes(16));
        return issued.CopyWithPrivateKey(key);
    }

    public static string SafeCommonName(string? value)
    {
        var safe = new string((value ?? "OpenFleet Console").Where(character => char.IsLetterOrDigit(character) || " ._-".Contains(character)).Take(64).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "OpenFleet Console" : safe;
    }

    private static X509Certificate2? FindInUserStore(string subject)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        return store.Certificates
            .Find(X509FindType.FindBySubjectDistinguishedName, subject, validOnly: false)
            .OfType<X509Certificate2>()
            .Where(certificate => certificate.HasPrivateKey && certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(1))
            .OrderByDescending(certificate => certificate.NotAfter)
            .FirstOrDefault();
    }

    private static X509Certificate2 PersistInUserStore(X509Certificate2 certificate)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var bytes = certificate.Export(X509ContentType.Pfx, password);
        using var imported = X509CertificateLoader.LoadPkcs12(bytes, password,
            X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(imported);
        return FindInUserStore(imported.Subject)
               ?? throw new CryptographicException("The Helper certificate could not be persisted in the Windows certificate store.");
    }
}
