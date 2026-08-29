using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using OpenFleetIT.Core;
using OpenFleetIT.Helper;

var state = HelperState.LoadOrCreate();
var builder = WebApplication.CreateBuilder(args);
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
Console.WriteLine($"Listening on https://0.0.0.0:{HomeProtocol.DefaultPort} (no firewall rule is created automatically).");
await app.RunAsync();
