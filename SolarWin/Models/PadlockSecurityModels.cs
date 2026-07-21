using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

// —— Devices / Sessions ——

/// <summary>GET /padlock/devices item (OpenAPI SnAuthClientWithSessions).</summary>
public sealed class SnAuthClientWithSessions
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("platform")]
    public ClientPlatform Platform { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("device_label")]
    public string? DeviceLabel { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("sessions")]
    public List<SnAuthSession>? Sessions { get; set; }
}

/// <summary>OpenAPI SnAuthSession.</summary>
public sealed class SnAuthSession
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("last_granted_at")]
    public DateTimeOffset? LastGrantedAt { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTimeOffset? ExpiredAt { get; set; }

    [JsonPropertyName("audiences")]
    public List<string>? Audiences { get; set; }

    [JsonPropertyName("scopes")]
    public List<string>? Scopes { get; set; }

    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("client_id")]
    public Guid? ClientId { get; set; }

    [JsonPropertyName("parent_session_id")]
    public Guid? ParentSessionId { get; set; }

    [JsonPropertyName("children_count")]
    public int ChildrenCount { get; set; }

    [JsonPropertyName("challenge_id")]
    public Guid? ChallengeId { get; set; }

    [JsonPropertyName("app_id")]
    public Guid? AppId { get; set; }
}

public sealed class DeviceLabelRequest
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

// —— Contacts ——

public sealed class ContactRequest
{
    [JsonPropertyName("type")]
    public AccountContactType Type { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; }
}

// —— Authorized apps ——

/// <summary>GET /padlock/authorized-apps item.</summary>
public sealed class AuthorizedAppResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("app_id")]
    public Guid? AppId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("app_slug")]
    public string? AppSlug { get; set; }

    [JsonPropertyName("app_name")]
    public string? AppName { get; set; }

    [JsonPropertyName("app_description")]
    public string? AppDescription { get; set; }

    [JsonPropertyName("scopes")]
    public List<string>? Scopes { get; set; }

    [JsonPropertyName("last_authorized_at")]
    public DateTimeOffset? LastAuthorizedAt { get; set; }

    [JsonPropertyName("last_used_at")]
    public DateTimeOffset? LastUsedAt { get; set; }
}

// —— API Keys ——

/// <summary>Flexible API key row (schema not fully documented).</summary>
public sealed class SnApiKey
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("key_prefix")]
    public string? KeyPrefix { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTimeOffset? ExpiredAt { get; set; }

    [JsonPropertyName("last_used_at")]
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Only present on create/rotate responses.</summary>
    [JsonPropertyName("secret")]
    public string? Secret { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonIgnore]
    public string DisplaySecret => Secret ?? Token ?? Key ?? string.Empty;

    [JsonIgnore]
    public string DisplayLabel => Label ?? Name ?? Prefix ?? KeyPrefix ?? Id.ToString("D")[..8];
}

public sealed class CreateApiKeyRequest
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("expired_at")]
    public DateTimeOffset? ExpiredAt { get; set; }
}

// —— Connections ——

/// <summary>GET /padlock/connections item (OpenAPI SnAccountConnection).</summary>
public sealed class SnAccountConnection
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("provided_identifier")]
    public string? ProvidedIdentifier { get; set; }

    [JsonPropertyName("last_used_at")]
    public DateTimeOffset? LastUsedAt { get; set; }

    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonIgnore]
    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(Provider) ? "已连接账户" : Provider!;

    [JsonIgnore]
    public string DisplaySubtitle =>
        string.IsNullOrWhiteSpace(ProvidedIdentifier)
            ? (LastUsedAt?.ToLocalTime().ToString("g") ?? string.Empty)
            : ProvidedIdentifier!;
}

public sealed class ConnectionVisibilityRequest
{
    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; }
}

// —— QR Login ——

/// <summary>OpenAPI QrLoginStatus (integer enum).</summary>
public enum QrLoginStatus
{
    Pending = 0,
    Scanned = 1,
    Approved = 2,
    Declined = 3,
    Expired = 4,
}

public sealed class QrGenerateRequest
{
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("platform")]
    public ClientPlatform Platform { get; set; } = ClientPlatform.Windows;

    [JsonPropertyName("audiences")]
    public List<string>? Audiences { get; set; }

    [JsonPropertyName("scopes")]
    public List<string>? Scopes { get; set; }
}

public sealed class QrGenerateResponse
{
    [JsonPropertyName("qr_challenge_id")]
    public Guid QrChallengeId { get; set; }

    [JsonPropertyName("auth_challenge_id")]
    public Guid? AuthChallengeId { get; set; }

    [JsonPropertyName("qr_data")]
    public string? QrData { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("expires_in_seconds")]
    public int ExpiresInSeconds { get; set; }
}

public sealed class QrStatusResponse
{
    [JsonPropertyName("qr_challenge_id")]
    public Guid QrChallengeId { get; set; }

    [JsonPropertyName("auth_challenge_id")]
    public Guid? AuthChallengeId { get; set; }

    /// <summary>OpenAPI QrLoginStatus integer (also accepts name strings).</summary>
    [JsonPropertyName("status")]
    public QrLoginStatus Status { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("approved_at")]
    public DateTimeOffset? ApprovedAt { get; set; }

    [JsonPropertyName("approved_device_id")]
    public string? ApprovedDeviceId { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("platform")]
    public ClientPlatform? Platform { get; set; }

    [JsonIgnore]
    public bool IsApproved =>
        Status is QrLoginStatus.Approved || ApprovedAt is not null;

    [JsonIgnore]
    public bool IsDeclined => Status is QrLoginStatus.Declined;

    [JsonIgnore]
    public bool IsExpired =>
        Status is QrLoginStatus.Expired
        || (ExpiresAt is { } exp && exp < DateTimeOffset.UtcNow);

    [JsonIgnore]
    public bool IsTerminal => IsApproved || IsDeclined || IsExpired;
}

// —— Passkey / WebAuthn ——

/// <summary>Opaque WebAuthn options from passkey start endpoints.</summary>
public sealed class PasskeyCeremonyOptions
{
    /// <summary>Raw JSON body (options) for WebAuthn APIs.</summary>
    public string? RawJson { get; set; }

    [JsonPropertyName("challenge")]
    public string? Challenge { get; set; }

    [JsonPropertyName("rpId")]
    public string? RpId { get; set; }

    [JsonPropertyName("timeout")]
    public long? Timeout { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class PasskeyCompleteRequest
{
    /// <summary>Serialized publicKeyCredential JSON from the authenticator.</summary>
    [JsonPropertyName("credential")]
    public JsonElement? Credential { get; set; }

    [JsonPropertyName("response")]
    public JsonElement? Response { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("rawId")]
    public string? RawId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; } = "public-key";

    [JsonPropertyName("clientDataJSON")]
    public string? ClientDataJson { get; set; }

    [JsonPropertyName("authenticatorData")]
    public string? AuthenticatorData { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("userHandle")]
    public string? UserHandle { get; set; }
}

// —— Multi-factor login state ——

/// <summary>Raised when password is OK but more challenge steps remain.</summary>
public sealed class MultiFactorRequiredException : Exception
{
    public MultiFactorRequiredException(
        Guid challengeId,
        IReadOnlyList<SnAccountAuthFactor> factors,
        SnAuthChallenge challenge)
        : base("需要额外验证步骤才能完成登录。")
    {
        ChallengeId = challengeId;
        Factors = factors;
        Challenge = challenge;
    }

    public Guid ChallengeId { get; }

    public IReadOnlyList<SnAccountAuthFactor> Factors { get; }

    public SnAuthChallenge Challenge { get; }
}
