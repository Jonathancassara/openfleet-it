using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace OpenFleetIT.App;

public static class ActionLogService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenFleetIT");
    private static readonly string LogPath = Path.Combine(LogDirectory, "actions.jsonl");

    public static async Task AppendAsync(string category, string target, string action, string result, string details = "")
    {
        await Gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var previousHash = (await ReadUnsafeAsync()).LastOrDefault()?.Hash ?? "GENESIS";
            var timestamp = DateTimeOffset.Now;
            var hash = ComputeHash(timestamp, category, target, action, result, details, previousHash);
            var entry = new ActionLogEntry(timestamp, category, target, action, result, details, previousHash, hash, true);
            await File.AppendAllTextAsync(LogPath, JsonSerializer.Serialize(entry) + Environment.NewLine, new UTF8Encoding(false));
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<IReadOnlyList<ActionLogEntry>> ReadAsync()
    {
        await Gate.WaitAsync();
        try { return await ReadUnsafeAsync(); }
        finally { Gate.Release(); }
    }

    private static async Task<List<ActionLogEntry>> ReadUnsafeAsync()
    {
        var entries = new List<ActionLogEntry>();
        if (!File.Exists(LogPath)) return entries;
        var previousHash = "GENESIS";
        foreach (var line in await File.ReadAllLinesAsync(LogPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var stored = JsonSerializer.Deserialize<ActionLogEntry>(line);
                if (stored is null) continue;
                if (string.IsNullOrWhiteSpace(stored.Hash) || string.IsNullOrWhiteSpace(stored.PreviousHash))
                {
                    entries.Add(stored with { IntegrityValid = false });
                    previousHash = stored.Hash ?? "INVALID";
                    continue;
                }
                var expected = ComputeHash(stored.Timestamp, stored.Category, stored.Target, stored.Action,
                    stored.Result, stored.Details, stored.PreviousHash);
                var valid = stored.PreviousHash == previousHash && CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(stored.Hash));
                entries.Add(stored with { IntegrityValid = valid });
                previousHash = stored.Hash;
            }
            catch (JsonException)
            {
                entries.Add(new ActionLogEntry(DateTimeOffset.MinValue, "Integrity", "—", "Invalid entry", "Error",
                    "The journal contains an unreadable line.", previousHash, "INVALID", false));
                previousHash = "INVALID";
            }
        }
        return entries;
    }

    private static string ComputeHash(DateTimeOffset timestamp, string category, string target, string action,
        string result, string details, string previousHash)
    {
        var payload = string.Join('\u001f', timestamp.ToString("O"), category, target, action, result, details, previousHash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

public sealed record ActionLogEntry(DateTimeOffset Timestamp, string Category, string Target, string Action,
    string Result, string Details, string PreviousHash, string Hash, bool IntegrityValid)
{
    public string TimestampDisplay => Timestamp == DateTimeOffset.MinValue ? "—" : Timestamp.ToLocalTime().ToString("g");
    public string IntegrityDisplay => IntegrityValid ? LocalizationService.Text("Verified") : LocalizationService.Text("Invalid");
}
