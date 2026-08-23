using System.ComponentModel;
using System.Diagnostics;

namespace OpenFleetIT.App;

public static class SoftwareActionService
{
    private static readonly SemaphoreSlim ExecutionGate = new(1, 1);

    public static string PreviewMsi(string productCode, bool repair) => repair
        ? $"msiexec.exe /fa {productCode} /passive /norestart"
        : $"msiexec.exe /x {productCode} /passive /norestart";

    public static string PreviewWinget(string packageId) =>
        $"winget.exe upgrade --id {packageId} --exact --accept-source-agreements --accept-package-agreements --disable-interactivity";

    public static async Task<ActionExecutionResult> ExecuteMsiAsync(string productCode, bool repair,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(productCode, out _))
            return new ActionExecutionResult(false, -1, "Invalid MSI product code.");

        var arguments = repair
            ? new[] { "/fa", productCode, "/passive", "/norestart" }
            : new[] { "/x", productCode, "/passive", "/norestart" };
        return await ExecuteAsync("msiexec.exe", arguments, elevate: true, cancellationToken);
    }

    public static async Task<ActionExecutionResult> ExecuteWingetAsync(string executablePath, string packageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId) || packageId.Any(char.IsWhiteSpace))
            return new ActionExecutionResult(false, -1, "Invalid WinGet package ID.");
        return await ExecuteAsync(executablePath,
            ["upgrade", "--id", packageId, "--exact", "--accept-source-agreements", "--accept-package-agreements", "--disable-interactivity"],
            elevate: false, cancellationToken);
    }

    public static async Task<ActionExecutionResult> ExecutePowerCommandAsync(string target, PowerAction action,
        CancellationToken cancellationToken = default)
    {
        var local = target.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || target.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
                    || target is "." or "127.0.0.1" or "::1";
        if (action == PowerAction.LogOff && !local)
            return new ActionExecutionResult(false, -1, "Remote logoff is not supported by the alpha command transport.");

        var arguments = new List<string>();
        if (!local) { arguments.Add("/m"); arguments.Add($@"\\{target}"); }
        switch (action)
        {
            case PowerAction.LogOff: arguments.Add("/l"); break;
            case PowerAction.Shutdown: arguments.AddRange(["/s", "/t", "30", "/c", "OpenFleet IT alpha requested shutdown"]); break;
            case PowerAction.Reboot: arguments.AddRange(["/r", "/t", "30", "/c", "OpenFleet IT alpha requested reboot"]); break;
            case PowerAction.Abort: arguments.Add("/a"); break;
        }
        return await ExecuteAsync("shutdown.exe", arguments, elevate: !local, cancellationToken);
    }

    private static async Task<ActionExecutionResult> ExecuteAsync(string fileName, IEnumerable<string> arguments,
        bool elevate, CancellationToken cancellationToken)
    {
        if (!await ExecutionGate.WaitAsync(0, cancellationToken))
            return new ActionExecutionResult(false, -1, "Another administrative action is already running.");
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = elevate,
                CreateNoWindow = !elevate,
                Verb = elevate ? "runas" : string.Empty,
                RedirectStandardOutput = !elevate,
                RedirectStandardError = !elevate
            };
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var outputTask = elevate ? Task.FromResult(string.Empty) : process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = elevate ? Task.FromResult(string.Empty) : process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromMinutes(10), cancellationToken);
            }
            catch (TimeoutException)
            {
                process.Kill(entireProcessTree: true);
                return new ActionExecutionResult(false, -1, "The action exceeded the 10-minute timeout.");
            }
            var output = await outputTask;
            var error = await errorTask;
            var details = string.Join(Environment.NewLine, new[] { output.Trim(), error.Trim() }.Where(value => value.Length > 0));
            if (details.Length > 2000) details = details[..2000];
            return new ActionExecutionResult(process.ExitCode == 0, process.ExitCode, details);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return new ActionExecutionResult(false, 1223, "Administrator elevation was cancelled.");
        }
        catch (Win32Exception exception)
        {
            return new ActionExecutionResult(false, exception.NativeErrorCode, exception.Message);
        }
        finally
        {
            ExecutionGate.Release();
        }
    }
}

public enum PowerAction { LogOff, Shutdown, Reboot, Abort }
public sealed record ActionExecutionResult(bool Success, int ExitCode, string Details);
