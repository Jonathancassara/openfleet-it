using System.IO;
using System.Text.Json;

namespace OpenFleetIT.App;

public sealed class OpenFleetSettings
{
    public List<string> DomainSuffixes { get; set; } = [".entreprise.fr", ".coworking.fr"];
}

public static class SettingsStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenFleetIT");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public static async Task<OpenFleetSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new OpenFleetSettings();
            await using var stream = File.OpenRead(SettingsPath);
            return await JsonSerializer.DeserializeAsync<OpenFleetSettings>(stream) ?? new OpenFleetSettings();
        }
        catch (IOException)
        {
            return new OpenFleetSettings();
        }
        catch (JsonException)
        {
            return new OpenFleetSettings();
        }
    }

    public static async Task SaveAsync(OpenFleetSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, new JsonSerializerOptions { WriteIndented = true });
    }
}
