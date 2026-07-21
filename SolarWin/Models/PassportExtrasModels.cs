using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

// —— Fortune ——

/// <summary>GET /passport/fortune · /passport/fortune/random</summary>
public sealed class FortuneSaying
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }
}

// —— IP check ——

/// <summary>GET /passport/ip-check</summary>
public sealed class IpCheckResponse
{
    [JsonPropertyName("client_ip")]
    public string? ClientIp { get; set; }

    [JsonPropertyName("remote_ip")]
    public string? RemoteIp { get; set; }

    [JsonPropertyName("x_forwarded_for")]
    public string? XForwardedFor { get; set; }

    [JsonPropertyName("x_forwarded_proto")]
    public string? XForwardedProto { get; set; }

    [JsonPropertyName("x_forwarded_host")]
    public string? XForwardedHost { get; set; }

    [JsonPropertyName("x_real_ip")]
    public string? XRealIp { get; set; }

    [JsonPropertyName("cf_connecting_ip")]
    public string? CfConnectingIp { get; set; }

    [JsonPropertyName("geo")]
    public GeoIpResponse? Geo { get; set; }

    [JsonPropertyName("headers")]
    public string? Headers { get; set; }
}

/// <summary>GET /passport/ip-check/geo</summary>
public sealed class GeoIpResponse
{
    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("subdivision")]
    public string? Subdivision { get; set; }

    [JsonPropertyName("subdivision_code")]
    public string? SubdivisionCode { get; set; }

    [JsonPropertyName("continent_code")]
    public string? ContinentCode { get; set; }

    [JsonPropertyName("time_zone")]
    public string? TimeZone { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }
}

// —— Notable days (user calendar notable) ——

/// <summary>GET/POST /passport/notable-days (OpenAPI SnNotableDay).</summary>
public sealed class SnNotableDay
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("local_name")]
    public string? LocalName { get; set; }

    [JsonPropertyName("localizable_key")]
    public string? LocalizableKey { get; set; }

    [JsonPropertyName("start_date")]
    public DateTimeOffset? StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateTimeOffset? EndDate { get; set; }

    [JsonPropertyName("is_all_day")]
    public bool IsAllDay { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("is_recurring")]
    public bool IsRecurring { get; set; }

    [JsonPropertyName("recurrence_pattern")]
    public string? RecurrencePattern { get; set; }

    [JsonPropertyName("is_period")]
    public bool IsPeriod { get; set; }
}

/// <summary>POST /passport/notable-days body.</summary>
public sealed class NotableDayRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("local_name")]
    public string? LocalName { get; set; }

    [JsonPropertyName("start_date")]
    public DateTimeOffset StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateTimeOffset EndDate { get; set; }

    [JsonPropertyName("is_all_day")]
    public bool IsAllDay { get; set; } = true;

    [JsonPropertyName("region")]
    public string? Region { get; set; } = "CN";
}

// —— Rewind ——

/// <summary>GET /passport/rewind/me · /passport/rewind/{code}</summary>
public sealed class SnRewindPoint
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("sharable_code")]
    public string? SharableCode { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, JsonElement>? Data { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }
}

// —— Magic spells ——

/// <summary>OpenAPI MagicSpellType.</summary>
public enum MagicSpellType
{
    Unknown = 0,
    Activation = 1,
    PasswordReset = 2,
    EmailChange = 3,
    Other = 4,
}

/// <summary>GET /passport/spells/{spellWord} (OpenAPI SnMagicSpell).</summary>
public sealed class SnMagicSpell
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public MagicSpellType Type { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("affected_at")]
    public DateTimeOffset? AffectedAt { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }

    [JsonPropertyName("account_id")]
    public Guid? AccountId { get; set; }

    [JsonPropertyName("account")]
    public SnAccount? Account { get; set; }
}

/// <summary>POST /passport/spells/{spellWord}/apply</summary>
public sealed class MagicSpellApplyRequest
{
    [JsonPropertyName("new_password")]
    public string? NewPassword { get; set; }
}
