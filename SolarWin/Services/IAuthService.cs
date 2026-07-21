using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>Padlock challenge-response authentication (Node 2 + MFA / QR).</summary>
public interface IAuthService
{
    /// <summary>True when a non-expired access token is present (or refresh keeps session alive).</summary>
    bool IsAuthenticated { get; }

    SnAccount? CurrentAccount { get; }

    event EventHandler? AuthenticationStateChanged;

    /// <summary>Load vault tokens / session on app start.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Full Padlock challenge login: challenge → factors → password → (optional MFA) → token exchange.
    /// When more factors remain after password, throws <see cref="MultiFactorRequiredException"/>.
    /// </summary>
    Task<TokenExchangeResponse> LoginAsync(string accountName, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Continue an in-progress challenge with another factor secret (TOTP / email / SMS / recovery).
    /// Completes login and returns tokens when the challenge is finished.
    /// </summary>
    Task<TokenExchangeResponse> CompleteFactorAsync(
        Guid challengeId,
        Guid factorId,
        string secret,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ask Padlock to deliver a code for email/SMS factors (POST …/factors/{factorId}).
    /// </summary>
    Task RequestFactorCodeAsync(Guid challengeId, Guid factorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wait for an InApp / remote approval factor: poll challenge until completed, then exchange tokens.
    /// </summary>
    Task<TokenExchangeResponse> WaitForChallengeCompletionAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// OAuth 2.0 Device Code login (third-party / desktop friendly).
    /// Requires a registered public client_id. Reports user_code via <paramref name="onUserCode"/>.
    /// </summary>
    Task<TokenExchangeResponse> LoginWithDeviceCodeAsync(
        string clientId,
        Action<DeviceAuthorizationResponse> onUserCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// QR login: generate challenge → report QR payload → poll until phone approves → token exchange.
    /// </summary>
    Task<TokenExchangeResponse> LoginWithQrAsync(
        Action<QrGenerateResponse> onQrGenerated,
        CancellationToken cancellationToken = default);

    /// <summary>POST /padlock/accounts — register; does not auto-login.</summary>
    Task<SnAccount> RegisterAsync(AccountCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /padlock/auth/recover with recovery code + captcha → tokens.</summary>
    Task<TokenExchangeResponse> RecoverAsync(RecoveryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Passkey login: start options → <paramref name="runCeremony"/> returns assertion JSON → complete → token.
    /// </summary>
    Task<TokenExchangeResponse> LoginWithPasskeyAsync(
        Func<string, CancellationToken, Task<string?>> runCeremony,
        CancellationToken cancellationToken = default);

    /// <summary>OIDC social login via browser + loopback (github/google/…).</summary>
    Task<TokenExchangeResponse> LoginWithSocialAsync(
        string provider,
        CancellationToken cancellationToken = default);

    /// <summary>Refresh using stored refresh_token (grant_type=refresh_token).</summary>
    Task<TokenExchangeResponse> RefreshTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>POST /padlock/auth/logout then clear local vault tokens.</summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>Switch multi-account vault slot and re-initialize session.</summary>
    Task SwitchAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Return a usable access token from PasswordVault.
    /// Refreshes automatically when the access token is missing/expired and a refresh token exists.
    /// </summary>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);

    Task<SnAccount> GetMeAsync(CancellationToken cancellationToken = default);
}
