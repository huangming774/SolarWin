using System.Globalization;
using System.Text.Json;

namespace SolarWin.Services;

internal static class LuckinJson
{
    public static IReadOnlyList<JsonElement> FindEntities(JsonElement root, params string[] identityProperties)
    {
        var results = new List<JsonElement>();
        VisitEntities(root, identityProperties, results);
        return results;
    }

    public static bool TryFindProperty(JsonElement root, out JsonElement value, params string[] names)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in names)
            {
                if (root.TryGetProperty(name, out var direct) && direct.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
                {
                    value = direct.Clone();
                    return true;
                }
            }

            foreach (var property in root.EnumerateObject())
            {
                if (TryFindProperty(property.Value, out value, names))
                {
                    return true;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (TryFindProperty(item, out value, names))
                {
                    return true;
                }
            }
        }
        else if (TryParseEmbeddedJson(root, out var embedded) && TryFindProperty(embedded, out value, names))
        {
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// 瑞幸 MCP 业务错误包装为 {"code":非0,"msg":"...","success":false}，MCP 协议层 isError 仍为 false，
    /// 必须在这里把它转成异常，否则调用方会把失败响应当成功数据解析。
    /// </summary>
    public static void ThrowIfBusinessError(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return;

        var failed = false;
        if (root.TryGetProperty("success", out var success) && success.ValueKind is JsonValueKind.False)
        {
            failed = true;
        }
        else if (root.TryGetProperty("code", out var code))
        {
            if (code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var number) && number != 0)
            {
                failed = true;
            }
            else if (code.ValueKind == JsonValueKind.String
                && int.TryParse(code.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                && parsed != 0)
            {
                failed = true;
            }
        }

        if (!failed) return;

        var message = root.TryGetProperty("msg", out var msg) && msg.ValueKind == JsonValueKind.String
            ? msg.GetString()
            : null;
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "瑞幸服务返回错误" : message);
    }

    public static string String(JsonElement root, params string[] names)
        => TryFindProperty(root, out var value, names) ? value.ToString() : string.Empty;

    public static int Int32(JsonElement root, params string[] names)
    {
        if (!TryFindProperty(root, out var value, names)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : 0;
    }

    public static bool TryNumber(JsonElement root, out double number, params string[] names)
    {
        if (TryFindProperty(root, out var value, names))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out number)) return true;
            if (double.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)) return true;
            if (double.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.CurrentCulture, out number)) return true;
        }

        number = 0;
        return false;
    }

    public static double Number(JsonElement root, params string[] names)
        => TryNumber(root, out var number, names) ? number : 0;

    private static void VisitEntities(JsonElement value, IReadOnlyList<string> identityProperties, ICollection<JsonElement> results)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (identityProperties.Any(name => value.TryGetProperty(name, out var identity) && identity.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined))
            {
                results.Add(value.Clone());
                return;
            }

            foreach (var property in value.EnumerateObject())
            {
                VisitEntities(property.Value, identityProperties, results);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                VisitEntities(item, identityProperties, results);
            }
        }
        else if (TryParseEmbeddedJson(value, out var embedded))
        {
            VisitEntities(embedded, identityProperties, results);
        }
    }

    private static bool TryParseEmbeddedJson(JsonElement value, out JsonElement embedded)
    {
        embedded = default;
        if (value.ValueKind != JsonValueKind.String) return false;
        var text = value.GetString()?.Trim();
        if (string.IsNullOrEmpty(text) || (text[0] != '{' && text[0] != '[')) return false;

        try
        {
            using var document = JsonDocument.Parse(text);
            embedded = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
