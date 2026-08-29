using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace OpenFleetIT.App;

public static class InventoryExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task ExportJsonAsync(string path, string target, IEnumerable<ApplicationItem> applications,
        CancellationToken cancellationToken = default)
    {
        var document = new InventoryExportDocument(
            SchemaVersion: 1,
            Target: target,
            CollectedAtUtc: DateTimeOffset.UtcNow,
            Applications: applications.Select(ToExportItem).ToArray());
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
    }

    public static async Task ExportCsvAsync(string path, IEnumerable<ApplicationItem> applications,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder("Name,Publisher,Version,Status,CanRepair,CanUpdate,CanUninstall\r\n");
        foreach (var application in applications)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Append(Csv(application.Name)).Append(',')
                .Append(Csv(application.Publisher)).Append(',')
                .Append(Csv(application.Version)).Append(',')
                .Append(Csv(application.Status)).Append(',')
                .Append(application.CanRepair.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(application.CanUpdate.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(application.CanUninstall.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        }
        await File.WriteAllTextAsync(path, builder.ToString(), new UTF8Encoding(true), cancellationToken);
    }

    public static string Csv(string value)
    {
        var escaped = (value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static InventoryApplicationExport ToExportItem(ApplicationItem application) => new(
        application.Name, application.Publisher, application.Version, application.Status,
        application.CanRepair, application.CanUpdate, application.CanUninstall);
}

public sealed record InventoryExportDocument(int SchemaVersion, string Target, DateTimeOffset CollectedAtUtc,
    IReadOnlyList<InventoryApplicationExport> Applications);

public sealed record InventoryApplicationExport(string Name, string Publisher, string Version, string Status,
    bool CanRepair, bool CanUpdate, bool CanUninstall);
