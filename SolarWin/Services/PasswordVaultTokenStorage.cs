using Windows.Security.Credentials;

namespace SolarWin.Services;

/// <summary>
/// Stores JWT access / refresh tokens in Windows PasswordVault.
/// Resource: "SolarWin"; users: access_token / refresh_token / access_token_expires_at.
/// Never logs token values.
/// </summary>
public sealed class PasswordVaultTokenStorage : ITokenStorage
{
    private const string ResourceName = "SolarWin";
    private const string AccessUserName = "access_token";
    private const string RefreshUserName = "refresh_token";
    private const string AccessExpiresUserName = "access_token_expires_at";

    private readonly PasswordVault _vault = new();

    public Task SaveTokensAsync(
        string accessToken,
        string? refreshToken,
        DateTimeOffset? accessExpiresAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        Upsert(AccessUserName, accessToken);

        if (string.IsNullOrEmpty(refreshToken))
        {
            TryRemove(RefreshUserName);
        }
        else
        {
            Upsert(RefreshUserName, refreshToken);
        }

        if (accessExpiresAt is null)
        {
            TryRemove(AccessExpiresUserName);
        }
        else
        {
            Upsert(AccessExpiresUserName, accessExpiresAt.Value.UtcDateTime.ToString("O"));
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryGet(AccessUserName));
    }

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryGet(RefreshUserName));
    }

    public Task<DateTimeOffset?> GetAccessTokenExpiresAtAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = TryGet(AccessExpiresUserName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Task.FromResult<DateTimeOffset?>(null);
        }

        return DateTimeOffset.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto)
            ? Task.FromResult<DateTimeOffset?>(dto)
            : Task.FromResult<DateTimeOffset?>(null);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryRemove(AccessUserName);
        TryRemove(RefreshUserName);
        TryRemove(AccessExpiresUserName);
        return Task.CompletedTask;
    }

    public async Task<bool> HasAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(token);
    }

    private void Upsert(string userName, string secret)
    {
        TryRemove(userName);
        _vault.Add(new PasswordCredential(ResourceName, userName, secret));
    }

    private string? TryGet(string userName)
    {
        try
        {
            var credential = _vault.Retrieve(ResourceName, userName);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void TryRemove(string userName)
    {
        try
        {
            var credential = _vault.Retrieve(ResourceName, userName);
            _vault.Remove(credential);
        }
        catch (Exception)
        {
            // Already absent.
        }
    }
}
