using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting.WindowsServices;
using OpenFleetIT.Core;
using OpenFleetIT.Helper;

if (args.Contains("--pairing-info", StringComparer.OrdinalIgnoreCase))
{
    using var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    try
    {
        var info = await client.GetFromJsonAsync<LocalPairingInfo>(
            $"https://localhost:{HomeProtocol.DefaultPort}/local/pairing");
        if (info is null) throw new InvalidDataException("The Helper returned no pairing information.");
        Console.WriteLine($"Device: {info.DeviceName}");
        Console.WriteLine($"Pairing code: {info.PairingCode}");
        Console.WriteLine($"Expires (local time): {info.ExpiresAtUtc.LocalDateTime:G}");
        Console.WriteLine($"Server SHA-256 fingerprint: {info.ServerCertificateSha256}");
        return;
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidDataException)
    {
        Console.Error.WriteLine($"OpenFleet Helper service is unavailable: {exception.Message}");
        Environment.ExitCode = 2;
        return;
    }
}

var state = HelperState.LoadOrCreate();
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(options => options.ServiceName = "OpenFleet IT Helper");
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Any, HomeProtocol.DefaultPort, listen =>
{
    listen.UseHttps(new HttpsConnectionAdapterOptions
    {
        ServerCertificate = state.ServerCertificate,
        ClientCertificateMode = ClientCertificateMode.AllowCertificate,
        ClientCertificateValidation = (certificate, _, _) => certificate is not null && state.IsPaired(certificate)
    });
}));

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    if (context.Request.ContentLength > 32_768)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return;
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(state.Health()));
app.MapGet("/local/pairing", (HttpContext context) =>
{
    var remoteAddress = context.Connection.RemoteIpAddress;
    return remoteAddress is not null && IPAddress.IsLoopback(remoteAddress)
        ? Results.Ok(state.LocalPairingInfo())
        : Results.NotFound();
});
app.MapPost("/v1/pair", (PairingRequest request) => state.Pair(request));
app.MapGet("/v1/inventory", (HttpContext context) =>
{
    var certificate = context.Connection.ClientCertificate;
    return certificate is null || !state.IsPaired(certificate)
        ? Results.Unauthorized()
        : Results.Ok(InventoryCollector.Collect(state.DeviceId));
});

Console.WriteLine($"OpenFleet Helper {state.HelperVersion}");
Console.WriteLine($"Device: {Environment.MachineName}");
Console.WriteLine($"Pairing code (valid 5 minutes): {state.PairingCode}");
Console.WriteLine($"Verify this server SHA-256 fingerprint in the console: {state.ServerFingerprint}");
Console.WriteLine($"Listening on https://0.0.0.0:{HomeProtocol.DefaultPort}.");
await app.RunAsync();
