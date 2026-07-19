using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>OAuth 2.0 device authorization response (RFC 8628).</summary>
public sealed class DeviceAuthorizationResponse
{
    [JsonPropertyName("device_code")]
    public string? DeviceCode { get; set; }

    [JsonPropertyName("user_code")]
    public string? UserCode { get; set; }

    [JsonPropertyName("verification_uri")]
    public string? VerificationUri { get; set; }

    [JsonPropertyName("verification_uri_complete")]
    public string? VerificationUriComplete { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("interval")]
    public int Interval { get; set; } = 5;
}

/// <summary>
/// OIDC token endpoint JSON. Field names differ from Padlock TokenExchangeResponse:
/// access_token vs token.
/// </summary>
public sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }

    [JsonPropertyName("refresh_expires_in")]
    public long RefreshExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    public string? ResolvedAccessToken => AccessToken ?? Token;

    public TokenExchangeResponse ToTokenExchange() => new()
    {
        Token = ResolvedAccessToken,
        RefreshToken = RefreshToken,
        ExpiresIn = ExpiresIn,
        RefreshExpiresIn = RefreshExpiresIn,
    };
}
