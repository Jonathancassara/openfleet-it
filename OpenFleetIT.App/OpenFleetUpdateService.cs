using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OpenFleetIT.App;

public static class OpenFleetUpdateService
{
    public const string CurrentVersion = "0.1.1-alpha.1";
    private static readonly Uri ReleasesUri = new("https://api.github.com/repos/Jonathancassara/openfleet-it/releases?per_page=10");

    public static async Task<OpenFleetUpdateResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OpenFleetIT", CurrentVersion));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        try
        {
            using var response = await client.GetAsync(ReleasesUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new OpenFleetUpdateResult(false, null, $"GitHub returned HTTP {(int)response.StatusCode}.");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var release = document.RootElement.EnumerateArray().FirstOrDefault(item =>
                !item.GetProperty("draft").GetBoolean() && item.TryGetProperty("tag_name", out _));
            if (release.ValueKind == JsonValueKind.Undefined)
                return new OpenFleetUpdateResult(false, null, null);
            var tag = release.GetProperty("tag_name").GetString()?.TrimStart('v');
            var url = release.TryGetProperty("html_url", out var html) ? html.GetString() : null;
            return new OpenFleetUpdateResult(!string.Equals(tag, CurrentVersion, StringComparison.OrdinalIgnoreCase), tag, null, url);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new OpenFleetUpdateResult(false, null, exception.Message);
        }
    }
}

public sealed record OpenFleetUpdateResult(bool UpdateAvailable, string? LatestVersion, string? Error, string? ReleaseUrl = null);
