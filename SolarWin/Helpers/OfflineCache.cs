using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Helpers;

/// <summary>
/// Disk-backed offline cache under %LOCALAPPDATA%\SolarWin\cache.
/// Stores JSON payloads with optional TTL for multi-session reuse.
/// </summary>
public static class OfflineCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static void SetJson<T>(string key, T value, TimeSpan? ttl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        AppPaths.EnsureDirectories();
        var envelope = new CacheEnvelope
        {
            SavedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = ttl is { } t ? DateTimeOffset.UtcNow.Add(t) : null,
            Payload = JsonSerializer.Serialize(value, JsonOptions),
        };
        var path = PathFor(key);
        File.WriteAllText(path, JsonSerializer.Serialize(envelope, JsonOptions));
    }

    public static bool TryGetJson<T>(string key, out T? value, bool allowExpired = false)
    {
        value = default;
        try
        {
            var path = PathFor(key);
            if (!File.Exists(path))
            {
                return false;
            }

            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(File.ReadAllText(path), JsonOptions);
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.Payload))
            {
                return false;
            }

            if (!allowExpired
                && envelope.ExpiresAtUtc is { } exp
                && exp < DateTimeOffset.UtcNow)
            {
                return false;
            }

            value = JsonSerializer.Deserialize<T>(envelope.Payload, JsonOptions);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    public static void Remove(string key)
    {
        try
        {
            var path = PathFor(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }

    public static void ClearAll()
    {
        try
        {
            if (Directory.Exists(AppPaths.CacheDirectory))
            {
                foreach (var f in Directory.EnumerateFiles(AppPaths.CacheDirectory))
                {
                    try { File.Delete(f); } catch { /* ignore */ }
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    public static long EstimateSizeBytes()
    {
        try
        {
            if (!Directory.Exists(AppPaths.CacheDirectory))
            {
                return 0;
            }

            return Directory.EnumerateFiles(AppPaths.CacheDirectory)
                .Select(f => new FileInfo(f).Length)
                .Sum();
        }
        catch
        {
            return 0;
        }
    }

    private static string PathFor(string key)
    {
        var safe = string.Concat(key.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        return Path.Combine(AppPaths.CacheDirectory, safe + ".json");
    }

    private sealed class CacheEnvelope
    {
        public DateTimeOffset SavedAtUtc { get; set; }

        public DateTimeOffset? ExpiresAtUtc { get; set; }

        public string? Payload { get; set; }
    }
}
