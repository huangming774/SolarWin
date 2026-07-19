using System.Text.Json;

namespace SolarWin.Helpers;

/// <summary>Parse Solar Network / OAuth error JSON bodies into a short user-facing message.</summary>
public static class ApiErrorParser
{
    public static string? TryGetMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Padlock style: { "code":"...", "message":"...", "status":400 }
            if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
            {
                var text = msg.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (root.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(code.GetString()))
                    {
                        return $"{text} ({code.GetString()})";
                    }

                    return text;
                }
            }

            // OAuth style: { "error":"...", "error_description":"..." }
            if (root.TryGetProperty("error_description", out var desc) && desc.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(desc.GetString()))
            {
                return desc.GetString();
            }

            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(err.GetString()))
            {
                return err.GetString();
            }
        }
        catch (JsonException)
        {
            // not JSON
        }

        var trimmed = responseBody.Trim();
        return trimmed.Length <= 240 ? trimmed : trimmed[..240] + "…";
    }
}
