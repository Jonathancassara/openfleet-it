using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace OpenFleetIT.App;

public static class MsixInventoryService
{
    public static async Task<IReadOnlyList<MsixPackage>> GetAsync(CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("Get-AppxPackage | Select-Object Name,Publisher,Version,Architecture,PackageFullName | ConvertTo-Json -Compress");
        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                return [];
            }
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0) throw new InvalidOperationException(error.Trim());
            if (string.IsNullOrWhiteSpace(output)) return [];
            using var document = JsonDocument.Parse(output);
            var elements = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().ToArray()
                : [document.RootElement];
            return elements.Select(element => new MsixPackage(
                    Read(element, "Name"), Read(element, "Publisher"), Read(element, "Version"),
                    Read(element, "Architecture"), Read(element, "PackageFullName")))
                .Where(package => !string.IsNullOrWhiteSpace(package.Name))
                .OrderBy(package => package.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch (Win32Exception)
        {
            return [];
        }
    }

    private static string Read(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.ToString() : string.Empty;
}

public sealed record MsixPackage(string Name, string Publisher, string Version, string Architecture, string PackageFullName);
