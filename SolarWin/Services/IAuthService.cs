using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>Padlock challenge-response authentication (Node 2).</summary>
public interface IAuthService
{
    /// <summary>True when a non-expired access token is present (or refresh keeps session alive).</summary>
    bool IsAuthenticated { get; }

    SnAccount? CurrentAccount { get; }

    event EventHandler? AuthenticationStateChanged;

    /// <summary>Load vault tokens / session on app start.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Full Padlock challenge login: challenge → poll → factors → password → token exchange.
    /// Returns the token pair; secrets are stored in PasswordVault (never logged).
    /// </summary>
    Task<TokenExchangeResponse> LoginAsync(string accountName, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// OAuth 2.0 Device Code login (third-party / desktop friendly).
    /// Requires a registered public client_id. Reports user_code via <paramref name="onUserCode"/>.
    /// </summary>
    Task<TokenExchangeResponse> LoginWithDeviceCodeAsync(
        string clientId,
        Action<DeviceAuthorizationResponse> onUserCode,
        CancellationToken cancellationToken = default);

    /// <summary>Refresh using stored refresh_token (grant_type=refresh_token).</summary>
    Task<TokenExchangeResponse> RefreshTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>POST /padlock/auth/logout then clear local vault tokens.</summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Return a usable access token from PasswordVault.
    /// Refreshes automatically when the access token is missing/expired and a refresh token exists.
    /// </summary>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);

    Task<SnAccount> GetMeAsync(CancellationToken cancellationToken = default);
}
