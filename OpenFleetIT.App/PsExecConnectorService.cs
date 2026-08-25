using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace OpenFleetIT.App;

public static partial class PsExecConnectorService
{
    private const string ProbeMarker = "OPENFLEET_PSEXEC_READY";

    public static async Task<PsExecProbeResult> TestAsync(string executablePath, string target,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(executablePath) || !Path.GetFileName(executablePath).Equals("PsExec.exe", StringComparison.OrdinalIgnoreCase))
            return new PsExecProbeResult(false, "The selected PsExec.exe file does not exist.");
        if (!TargetPattern().IsMatch(target))
            return new PsExecProbeResult(false, "The target hostname or IP address is invalid.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add($@"\\{target}");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add("10");
        startInfo.ArgumentList.Add("-nobanner");
        startInfo.ArgumentList.Add("cmd.exe");
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add($"echo {ProbeMarker}");

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                return new PsExecProbeResult(false, "PsExec did not respond within 20 seconds.");
            }

            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode == 0 && output.Contains(ProbeMarker, StringComparison.Ordinal))
                return new PsExecProbeResult(true, "PsExec reached the remote device using the current Windows identity.");

            var details = string.Join(" ", new[] { error.Trim(), output.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (details.Length > 500) details = details[..500];
            return new PsExecProbeResult(false, string.IsNullOrWhiteSpace(details)
                ? $"PsExec exited with code {process.ExitCode}."
                : details);
        }
        catch (Win32Exception exception)
        {
            return new PsExecProbeResult(false, exception.Message);
        }
    }

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._:-]{0,254}$")]
    private static partial Regex TargetPattern();
}

public sealed record PsExecProbeResult(bool Success, string Details);
