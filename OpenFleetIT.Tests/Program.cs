using OpenFleetIT.App;

var failures = new List<string>();

Test("WinGet English table", () =>
{
    const string output = """
Name                          Id                         Version    Available  Source
------------------------------------------------------------------------------------
7-Zip 24.09                  7zip.7zip                  24.09      25.01      winget
Microsoft PowerToys          Microsoft.PowerToys       0.90.0     0.91.1     winget
2 upgrades available.
""";
    var updates = WingetUpdateService.Parse(output);
    Equal(2, updates.Count);
    Equal("7zip.7zip", updates[0].Id);
    Equal("25.01", updates[0].AvailableVersion);
});

Test("WinGet localized headers", () =>
{
    const string output = """
Nom                           ID                         Version    Disponible  Source
------------------------------------------------------------------------------------
VLC media player              VideoLAN.VLC              3.0.20     3.0.21      winget
1 mise à niveau disponible.
""";
    var updates = WingetUpdateService.Parse(output);
    Equal(1, updates.Count);
    Equal("VideoLAN.VLC", updates[0].Id);
});

Test("WinGet empty output", () => Equal(0, WingetUpdateService.Parse("No upgrades available.").Count));
Test("WinGet ignores malformed rows and ANSI sequences", () =>
{
    const string output = "\u001b[32mName Id Version Available Source\u001b[0m\r\n" +
                          "-----------------------------------------------\r\n" +
                          "Broken row\r\n" +
                          "VLC media player              VideoLAN.VLC              3.0.20     3.0.21      winget\r\n";
    var updates = WingetUpdateService.Parse(output);
    Equal(1, updates.Count);
    Equal("VideoLAN.VLC", updates[0].Id);
});
Test("MSI command preview", () => Equal(
    "msiexec.exe /x {11111111-1111-1111-1111-111111111111} /passive /norestart",
    SoftwareActionService.PreviewMsi("{11111111-1111-1111-1111-111111111111}", false)));
Test("WinGet exact command preview", () => Equal(
    "winget.exe upgrade --id VideoLAN.VLC --exact --accept-source-agreements --accept-package-agreements --disable-interactivity",
    SoftwareActionService.PreviewWinget("VideoLAN.VLC")));
Test("CSV export escapes commas and quotes", () => Equal(
    "\"ACME, \"\"Suite\"\"\"",
    InventoryExportService.Csv("ACME, \"Suite\"")));
Test("Coordinator prevents duplicate package action", () =>
{
    var coordinator = new ActionExecutionCoordinator();
    Equal(true, coordinator.TryBegin("winget:VideoLAN.VLC", CancellationToken.None, out var first));
    Equal(false, coordinator.TryBegin("winget:videolan.vlc", CancellationToken.None, out var duplicate));
    Equal(null, duplicate);
    first!.Dispose();
    Equal(true, coordinator.TryBegin("winget:VideoLAN.VLC", CancellationToken.None, out var next));
    next!.Dispose();
});
Test("Coordinator cancels active actions", () =>
{
    var coordinator = new ActionExecutionCoordinator();
    coordinator.TryBegin("power:localhost", CancellationToken.None, out var lease);
    coordinator.CancelAll();
    Equal(true, lease!.Token.IsCancellationRequested);
    lease.Dispose();
});
Test("Pairing code format", () =>
{
    var code = OpenFleetIT.Core.PairingCode.Create();
    Equal(true, OpenFleetIT.Core.PairingCode.IsValidFormat(code));
    Equal(true, OpenFleetIT.Core.PairingCode.FixedTimeEquals(code, code));
    Equal(false, OpenFleetIT.Core.PairingCode.FixedTimeEquals(code, "000000" == code ? "000001" : "000000"));
});
Test("Home Helper fingerprint normalization", () => Equal(
    "00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF",
    HomeHelperClient.NormalizeFingerprint("00:11:22:33:44:55:66:77:88:99:aa:bb:cc:dd:ee:ff:00:11:22:33:44:55:66:77:88:99:aa:bb:cc:dd:ee:ff")));
Test("Home Helper rejects short fingerprint", () => Throws<ArgumentException>(() =>
    HomeHelperClient.NormalizeFingerprint("0011")));
Test("Home Helper accepts IPv6 target", () => Equal("::1", HomeHelperClient.NormalizeHost("::1")));

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} test(s) failed:");
    foreach (var failure in failures) Console.Error.WriteLine($"- {failure}");
    return 1;
}

Console.WriteLine("All OpenFleet IT alpha tests passed.");
return 0;

void Test(string name, Action action)
{
    try { action(); }
    catch (Exception exception) { failures.Add($"{name}: {exception.Message}"); }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected '{expected}', received '{actual}'.");
}

static void Throws<TException>(Action action) where TException : Exception
{
    try { action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}
