using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Helpers;

/// <summary>
/// Flexible Instant / timestamp reader.
/// NodaTime Instant and mixed backends may send ISO string, unix seconds/ms, or object.
/// </summary>
public sealed class FlexibleDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                {
                    return null;
                }

                if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var dto))
                {
                    return dto;
                }

                // numeric string
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                {
                    return FromUnix(n);
                }

                return null;
            }
            case JsonTokenType.Number:
            {
                if (reader.TryGetInt64(out var n))
                {
                    return FromUnix(n);
                }

                if (reader.TryGetDouble(out var d))
                {
                    return FromUnix((long)d);
                }

                return null;
            }
            case JsonTokenType.StartObject:
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                var root = doc.RootElement;
                // Common shapes: { "seconds": n }, { "epoch_seconds": n }, { "value": "iso" }
                foreach (var key in new[] { "seconds", "epoch_seconds", "epochSeconds", "unix", "epoch" })
                {
                    if (root.TryGetProperty(key, out var p) && p.TryGetInt64(out var sec))
                    {
                        return DateTimeOffset.FromUnixTimeSeconds(sec);
                    }
                }

                foreach (var key in new[] { "milliseconds", "epoch_milliseconds", "epochMilliseconds", "ms" })
                {
                    if (root.TryGetProperty(key, out var p) && p.TryGetInt64(out var ms))
                    {
                        return DateTimeOffset.FromUnixTimeMilliseconds(ms);
                    }
                }

                if (root.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(v.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var parsed))
                {
                    return parsed;
                }

                // NodaTime sometimes uses nested ticks
                if (root.TryGetProperty("ticks", out var ticksEl) && ticksEl.TryGetInt64(out var ticks))
                {
                    try
                    {
                        return new DateTimeOffset(ticks, TimeSpan.Zero);
                    }
                    catch
                    {
                        return null;
                    }
                }

                return null;
            }
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("O"));
        }
    }

    private static DateTimeOffset FromUnix(long n)
    {
        // Heuristic: ms if > year 2001 in seconds (~1e12)
        try
        {
            return n > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(n)
                : DateTimeOffset.FromUnixTimeSeconds(n);
        }
        catch
        {
            return DateTimeOffset.UnixEpoch;
        }
    }
}

/// <summary>
/// API often sends <c>null</c> for optional bools (e.g. publisher.gatekept_follows).
/// STJ rejects null → non-nullable bool and drops the entire parent object.
/// </summary>
public sealed class FlexibleBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return false;
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.String:
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                {
                    return false;
                }

                if (bool.TryParse(s, out var b))
                {
                    return b;
                }

                if (s is "1" or "yes" or "YES" or "on" or "ON")
                {
                    return true;
                }

                return false;
            }
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var n) && n != 0;
            default:
                reader.Skip();
                return false;
        }
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => writer.WriteBooleanValue(value);
}

/// <summary>Null / empty numeric strings → 0 for non-nullable ints.</summary>
public sealed class FlexibleInt32Converter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return 0;
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var n) ? n : (int)reader.GetDouble();
            case JsonTokenType.String:
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                {
                    return 0;
                }

                return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
            }
            default:
                reader.Skip();
                return 0;
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

/// <summary>Accept Guid as string; empty / invalid → Guid.Empty.</summary>
public sealed class FlexibleGuidConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return Guid.Empty;
            case JsonTokenType.String:
            {
                var s = reader.GetString();
                return Guid.TryParse(s, out var g) ? g : Guid.Empty;
            }
            default:
                reader.Skip();
                return Guid.Empty;
        }
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}

/// <summary>Accept Guid? as string; empty / invalid → null.</summary>
public sealed class FlexibleNullableGuidConverter : JsonConverter<Guid?>
{
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                {
                    return null;
                }

                return Guid.TryParse(s, out var g) ? g : null;
            }
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.ToString());
        }
    }
}

/// <summary>Base64 byte[]; invalid / empty → null (avoids ciphertext decode failures).</summary>
public sealed class FlexibleByteArrayConverter : JsonConverter<byte[]?>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                {
                    return null;
                }

                try
                {
                    return Convert.FromBase64String(s);
                }
                catch
                {
                    return null;
                }
            }
            case JsonTokenType.StartArray:
            {
                var list = new List<byte>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out var b))
                    {
                        list.Add(b);
                    }
                }

                return list.Count == 0 ? null : list.ToArray();
            }
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, byte[]? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteBase64StringValue(value);
        }
    }
}

/// <summary>
/// Unknown enum integers still map; invalid strings → default.
/// Creates a separate converter for <c>T</c> vs <c>T?</c> (STJ requires exact type match).
/// </summary>
public sealed class FlexibleEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsEnum
           || (Nullable.GetUnderlyingType(typeToConvert)?.IsEnum ?? false);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var underlying = Nullable.GetUnderlyingType(typeToConvert);
        if (underlying is { IsEnum: true })
        {
            // Must return JsonConverter<T?> — not JsonConverter<T>.
            var nullableConverterType = typeof(FlexibleNullableEnumConverter<>).MakeGenericType(underlying);
            return (JsonConverter)Activator.CreateInstance(nullableConverterType)!;
        }

        if (!typeToConvert.IsEnum)
        {
            throw new NotSupportedException($"Type {typeToConvert} is not an enum.");
        }

        var converterType = typeof(FlexibleEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class FlexibleEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => ReadEnum<T>(ref reader);

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteNumberValue(Convert.ToInt32(value));
    }

    private sealed class FlexibleNullableEnumConverter<T> : JsonConverter<T?> where T : struct, Enum
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.Null)
            {
                return null;
            }

            return ReadEnum<T>(ref reader);
        }

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteNumberValue(Convert.ToInt32(value.Value));
        }
    }

    private static T ReadEnum<T>(ref Utf8JsonReader reader) where T : struct, Enum
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n))
        {
            return (T)Enum.ToObject(typeof(T), n);
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s))
            {
                return default;
            }

            if (Enum.TryParse<T>(s, ignoreCase: true, out var byName))
            {
                return byName;
            }

            if (int.TryParse(s, out var ni))
            {
                return (T)Enum.ToObject(typeof(T), ni);
            }
        }

        if (reader.TokenType is JsonTokenType.Null)
        {
            return default;
        }

        reader.Skip();
        return default;
    }
}
