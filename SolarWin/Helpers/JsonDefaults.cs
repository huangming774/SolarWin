using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Helpers;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for Solar Network API payloads (snake_case).
/// Includes flexible Instant / Guid / enum converters used by Messager & Passport.
/// </summary>
public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            // Prefer skipping bad nested properties when possible (STJ still fails on wrong root type).
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        };

        options.Converters.Add(new FlexibleDateTimeOffsetConverter());
        options.Converters.Add(new FlexibleBoolConverter());
        options.Converters.Add(new FlexibleInt32Converter());
        options.Converters.Add(new FlexibleGuidConverter());
        options.Converters.Add(new FlexibleNullableGuidConverter());
        options.Converters.Add(new FlexibleByteArrayConverter());
        options.Converters.Add(new FlexibleEnumConverterFactory());

        return options;
    }
}
