using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace OpenFleetIT.App;

public static partial class WingetUpdateService
{
    public static async Task<WingetUpdateResult> GetAvailableUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var executable = FindExecutable();
        if (executable is null)
            return new WingetUpdateResult(false, [], "WinGet is not installed or its application execution alias is disabled.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = "upgrade --accept-source-agreements --disable-interactivity",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
            }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                return new WingetUpdateResult(true, [], "WinGet did not respond within 60 seconds.");
            }
            var output = await standardOutput;
            var error = await standardError;
            var updates = Parse(output);

            if (updates.Count == 0 && process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                return new WingetUpdateResult(true, [], error.Trim());

            return new WingetUpdateResult(true, updates, null);
        }
        catch (Win32Exception)
        {
            return new WingetUpdateResult(false, [], "WinGet could not be started.");
        }
    }

    public static IReadOnlyList<WingetUpdate> Parse(string output)
    {
        var lines = AnsiEscapePattern().Replace(output, string.Empty)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n');
        var separatorIndex = Array.FindIndex(lines, line =>
        {
            var value = line.Trim();
            return value.Length >= 20 && value.All(character => character == '-');
        });
        if (separatorIndex < 1) return [];

        var results = new List<WingetUpdate>();
        for (var index = separatorIndex + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ColumnSeparatorPattern().Split(line.Trim()).Where(field => !string.IsNullOrWhiteSpace(field)).ToArray();
            if (fields.Length < 4) continue;

            var name = fields[0];
            var id = fields[1];
            var installedVersion = fields[2];
            var availableVersion = fields[3];
            var source = fields.Length > 4 ? fields[4] : string.Empty;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)
                || string.IsNullOrWhiteSpace(installedVersion) || string.IsNullOrWhiteSpace(availableVersion)) continue;

            results.Add(new WingetUpdate(name, id, installedVersion, availableVersion, source));
        }

        return results
            .GroupBy(update => update.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(update => update.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static string? FindExecutable()
    {
        var alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "winget.exe");
        if (File.Exists(alias)) return alias;

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(directory, "winget.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                // Ignore malformed PATH entries and keep searching.
            }
        }

        return null;
    }

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])")]
    private static partial Regex AnsiEscapePattern();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ColumnSeparatorPattern();
}

public sealed record WingetUpdate(string Name, string Id, string InstalledVersion, string AvailableVersion, string Source);
public sealed record WingetUpdateResult(bool IsWingetAvailable, IReadOnlyList<WingetUpdate> Updates, string? Error);
