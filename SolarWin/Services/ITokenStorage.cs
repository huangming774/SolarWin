namespace SolarWin.Services;

/// <summary>Secure local token store (Windows PasswordVault).</summary>
public interface ITokenStorage
{
    /// <summary>
    /// Persist access / refresh tokens. Does not log secrets.
    /// </summary>
    Task SaveTokensAsync(
        string accessToken,
        string? refreshToken,
        DateTimeOffset? accessExpiresAt,
        CancellationToken cancellationToken = default);

    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetAccessTokenExpiresAtAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    Task<bool> HasAccessTokenAsync(CancellationToken cancellationToken = default);
}
