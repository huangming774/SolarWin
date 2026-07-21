using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>POST /padlock/accounts (OpenAPI AccountCreateRequest).</summary>
public sealed class AccountCreateRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("nick")]
    public required string Nick { get; set; }

    [JsonPropertyName("email")]
    public required string Email { get; set; }

    [JsonPropertyName("password")]
    public required string Password { get; set; }

    [JsonPropertyName("captcha_token")]
    public required string CaptchaToken { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("affiliation_spell")]
    public string? AffiliationSpell { get; set; }
}

/// <summary>POST /padlock/auth/recover (OpenAPI RecoveryRequest).</summary>
public sealed class RecoveryRequest
{
    [JsonPropertyName("account")]
    public required string Account { get; set; }

    [JsonPropertyName("recovery_code")]
    public required string RecoveryCode { get; set; }

    [JsonPropertyName("captcha_token")]
    public required string CaptchaToken { get; set; }

    [JsonPropertyName("device_id")]
    public required string DeviceId { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    [JsonPropertyName("platform")]
    public ClientPlatform Platform { get; set; } = ClientPlatform.Windows;
}

/// <summary>GET /padlock/auth/captcha.</summary>
public sealed class CaptchaConfigResponse
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    /// <summary>hCaptcha / reCAPTCHA site key (OpenAPI field name api_key).</summary>
    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("site_key")]
    public string? SiteKey { get; set; }

    [JsonIgnore]
    public string ResolvedSiteKey => SiteKey ?? ApiKey ?? string.Empty;

    [JsonIgnore]
    public bool IsHCaptcha =>
        string.Equals(Provider, "hcaptcha", StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(Provider);
}

/// <summary>POST /padlock/auth/captcha/verify.</summary>
public sealed class CaptchaVerifyRequest
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

/// <summary>POST /padlock/auth/passkey/start.</summary>
public sealed class PasskeyLoginStartRequest
{
    [JsonPropertyName("platform")]
    public ClientPlatform Platform { get; set; } = ClientPlatform.Windows;

    [JsonPropertyName("device_id")]
    public required string DeviceId { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }
}

/// <summary>Response from POST /padlock/auth/passkey/start.</summary>
public sealed class PasskeyLoginStartResponse
{
    [JsonPropertyName("challenge")]
    public string? Challenge { get; set; }

    [JsonPropertyName("rp_id")]
    public string? RpId { get; set; }

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; }

    [JsonPropertyName("user_verification")]
    public string? UserVerification { get; set; }

    [JsonPropertyName("auth_challenge_id")]
    public Guid AuthChallengeId { get; set; }

    [JsonPropertyName("allow_credentials")]
    public List<PasskeyCredentialDescriptor>? AllowCredentials { get; set; }

    /// <summary>Some deployments nest options under publicKey / options.</summary>
    [JsonPropertyName("public_key")]
    public PasskeyLoginStartResponse? PublicKey { get; set; }
}

public sealed class PasskeyCredentialDescriptor
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("transports")]
    public List<string>? Transports { get; set; }
}

/// <summary>POST /padlock/auth/passkey/{id}/complete.</summary>
public sealed class PasskeyAuthenticationCompleteRequest
{
    [JsonPropertyName("credential_id")]
    public required string CredentialId { get; set; }

    [JsonPropertyName("client_data_json")]
    public required string ClientDataJson { get; set; }

    [JsonPropertyName("authenticator_data")]
    public required string AuthenticatorData { get; set; }

    [JsonPropertyName("signature")]
    public required string Signature { get; set; }

    [JsonPropertyName("user_handle")]
    public string? UserHandle { get; set; }
}

/// <summary>POST /padlock/factors/passkey/start body (optional).</summary>
public sealed class PasskeyRegistrationStartRequest
{
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }
}

/// <summary>POST /padlock/factors/passkey/complete.</summary>
public sealed class PasskeyRegistrationCompleteRequest
{
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("client_data_json")]
    public string? ClientDataJson { get; set; }

    [JsonPropertyName("attestation_object")]
    public string? AttestationObject { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

/// <summary>Contact verification body (client convention; OpenAPI body optional).</summary>
public sealed class ContactVerifyRequest
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

/// <summary>Social OIDC providers exposed on login UI.</summary>
public static class SocialLoginProviders
{
    public static readonly string[] All =
    [
        "github",
        "google",
        "discord",
        "microsoft",
        "apple",
    ];

    public static string DisplayName(string provider) => provider.ToLowerInvariant() switch
    {
        "github" => "GitHub",
        "google" => "Google",
        "discord" => "Discord",
        "microsoft" => "Microsoft",
        "apple" => "Apple",
        _ => provider,
    };
}
