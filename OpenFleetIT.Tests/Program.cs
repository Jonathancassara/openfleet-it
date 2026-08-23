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
Test("MSI command preview", () => Equal(
    "msiexec.exe /x {11111111-1111-1111-1111-111111111111} /passive /norestart",
    SoftwareActionService.PreviewMsi("{11111111-1111-1111-1111-111111111111}", false)));
Test("WinGet exact command preview", () => Equal(
    "winget.exe upgrade --id VideoLAN.VLC --exact --accept-source-agreements --accept-package-agreements --disable-interactivity",
    SoftwareActionService.PreviewWinget("VideoLAN.VLC")));

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
