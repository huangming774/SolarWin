using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Padlock auth challenge (OpenAPI SnAuthChallenge).</summary>
public sealed class SnAuthChallenge
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTimeOffset? ExpiredAt { get; set; }

    [JsonPropertyName("step_remain")]
    public int StepRemain { get; set; }

    [JsonPropertyName("step_total")]
    public int StepTotal { get; set; }

    [JsonPropertyName("failed_attempts")]
    public int FailedAttempts { get; set; }

    [JsonPropertyName("blacklist_factors")]
    public List<Guid>? BlacklistFactors { get; set; }

    [JsonPropertyName("audiences")]
    public List<string>? Audiences { get; set; }

    [JsonPropertyName("scopes")]
    public List<string>? Scopes { get; set; }

    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("platform")]
    public ClientPlatform Platform { get; set; }

    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("approved_at")]
    public DateTimeOffset? ApprovedAt { get; set; }

    [JsonPropertyName("declined_at")]
    public DateTimeOffset? DeclinedAt { get; set; }

    [JsonPropertyName("approved_by_session_id")]
    public Guid? ApprovedBySessionId { get; set; }

    [JsonIgnore]
    public bool IsCompleted => StepRemain <= 0;
}

/// <summary>POST /padlock/auth/challenge body (OpenAPI ChallengeRequest).</summary>
public sealed class ChallengeRequest
{
    [JsonPropertyName("platform")]
    public ClientPlatform Platform { get; set; }

    [JsonPropertyName("account")]
    public required string Account { get; set; }

    [JsonPropertyName("device_id")]
    public required string DeviceId { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("audiences")]
    public List<string>? Audiences { get; set; }

    [JsonPropertyName("scopes")]
    public List<string>? Scopes { get; set; }
}

/// <summary>PATCH /padlock/auth/challenge/{id} body (OpenAPI PerformChallengeRequest).</summary>
public sealed class PerformChallengeRequest
{
    [JsonPropertyName("factor_id")]
    public Guid FactorId { get; set; }

    [JsonPropertyName("password")]
    public required string Password { get; set; }
}

/// <summary>POST /padlock/auth/token body (OpenAPI TokenExchangeRequest).</summary>
public sealed class TokenExchangeRequest
{
    [JsonPropertyName("grant_type")]
    public string? GrantType { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

/// <summary>Auth factor on challenge (OpenAPI SnAccountAuthFactor).</summary>
public sealed class SnAccountAuthFactor
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public AccountAuthFactorType Type { get; set; }

    [JsonPropertyName("trustworthy")]
    public int Trustworthy { get; set; }

    [JsonPropertyName("enabled_at")]
    public DateTimeOffset? EnabledAt { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTimeOffset? ExpiredAt { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonIgnore]
    public bool IsEnabled => EnabledAt is not null && (ExpiredAt is null || ExpiredAt > DateTimeOffset.UtcNow);

    [JsonIgnore]
    public string TypeDisplayName => Type switch
    {
        AccountAuthFactorType.Password => "密码",
        AccountAuthFactorType.Email => "邮箱验证码",
        AccountAuthFactorType.PhoneCode => "短信验证码",
        AccountAuthFactorType.Totp => "TOTP / 验证器",
        AccountAuthFactorType.Passkey => "Passkey",
        AccountAuthFactorType.InApp => "应用内确认",
        AccountAuthFactorType.Recovery => "恢复码",
        AccountAuthFactorType.Oidc => "第三方登录",
        _ => Type.ToString(),
    };
}
