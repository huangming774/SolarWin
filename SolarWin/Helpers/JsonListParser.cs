using System.Text.Json;

namespace SolarWin.Helpers;

/// <summary>
/// Parse API list payloads that may be a bare array or wrapped object
/// (Solian web: data / items / rooms for /messager/chat).
/// </summary>
public static class JsonListParser
{
    public static List<T> ParseList<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            // Lenient: one bad item must not drop the whole sticker/pack list.
            return DeserializeArrayLenient<T>(root);
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in new[]
                     {
                         "data", "items", "rooms", "results", "content", "files", "messages",
                         "notifications", "posts", "events",
                     })
            {
                if (root.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    return DeserializeArrayLenient<T>(arr);
                }
            }

            // Single object mistaken for list
            try
            {
                var one = root.Deserialize<T>(JsonDefaults.Options);
                if (one is not null)
                {
                    return [one];
                }
            }
            catch (JsonException)
            {
                // ignore
            }
        }

        return [];
    }

    /// <summary>
    /// Deserialize each array element independently so one bad item does not fail the whole list.
    /// </summary>
    public static List<T> DeserializeArrayLenient<T>(JsonElement array)
    {
        var list = new List<T>();
        if (array.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var el in array.EnumerateArray())
        {
            try
            {
                var item = el.Deserialize<T>(JsonDefaults.Options);
                if (item is not null)
                {
                    list.Add(item);
                }
            }
            catch (JsonException)
            {
                // skip broken element
            }
            catch (NotSupportedException)
            {
                // converter / cycle issues — skip rather than empty the whole feed
            }
        }

        return list;
    }

    public static Dictionary<string, TValue> ParseStringKeyDictionary<TValue>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        }

        // Some deployments wrap summary: { "data": { "uuid": {...} } }
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            root = data;
        }
        else if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
        {
            root = items;
        }

        var map = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in root.EnumerateObject())
        {
            try
            {
                var value = prop.Value.Deserialize<TValue>(JsonDefaults.Options);
                if (value is not null)
                {
                    map[prop.Name] = value;
                }
            }
            catch (JsonException)
            {
                // skip bad summary entry
            }
        }

        return map;
    }
}
