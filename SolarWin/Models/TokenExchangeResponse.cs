using System.Text.Json.Serialization;

namespace SolarWin.Models;

/// <summary>Token pair response (OpenAPI TokenExchangeResponse).</summary>
public sealed class TokenExchangeResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }

    [JsonPropertyName("refresh_expires_in")]
    public long RefreshExpiresIn { get; set; }
}
