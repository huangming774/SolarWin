using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Storage;

namespace SolarWin.Helpers;

/// <summary>
/// String key-value settings that work both packaged (ApplicationData) and unpackaged.
/// Unpackaged processes have no package identity: ApplicationData.Current throws
/// InvalidOperationException, so fall back to a JSON file under %LOCALAPPDATA%\SolarWin.
/// </summary>
public static class SettingsStore
{
    private static readonly object Sync = new();
    private static readonly bool UseApplicationData = ProbeApplicationData();
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SolarWin",
        "settings.json");
    private static Dictionary<string, string>? _fileValues;

    public static string? GetString(string key)
    {
        if (UseApplicationData)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return values.TryGetValue(key, out var raw) && raw is string s ? s : null;
        }

        lock (Sync)
        {
            return LoadFile().TryGetValue(key, out var s) ? s : null;
        }
    }

    public static void SetString(string key, string value)
    {
        if (UseApplicationData)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
            return;
        }

        lock (Sync)
        {
            var values = LoadFile();
            values[key] = value;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(values, SettingsJsonContext.Default.DictionaryStringString));
            }
            catch (IOException)
            {
                // Best-effort persistence; settings are non-critical.
            }
        }
    }

    private static bool ProbeApplicationData()
    {
        try
        {
            _ = ApplicationData.Current.LocalSettings.Values.Count;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, string> LoadFile()
    {
        if (_fileValues is not null)
        {
            return _fileValues;
        }

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _fileValues = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.DictionaryStringString)
                              ?? new Dictionary<string, string>();
                return _fileValues;
            }
        }
        catch (JsonException)
        {
            // Corrupt file — start fresh.
        }
        catch (IOException)
        {
            // Unreadable file — start fresh.
        }

        _fileValues = new Dictionary<string, string>();
        return _fileValues;
    }
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class SettingsJsonContext : JsonSerializerContext;
