using Windows.Security.Credentials;

namespace SolarWin.Services;

/// <summary>
/// Stores JWT access / refresh tokens in Windows PasswordVault.
/// Active slot: Resource "SolarWin" users access_token / refresh_token / access_token_expires_at.
/// Per-account slots: Resource "SolarWin:{accountId:D}".
/// Never logs token values.
/// </summary>
public sealed class PasswordVaultTokenStorage : ITokenStorage, IMultiAccountTokenStorage
{
    private const string ActiveResource = "SolarWin";
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

        Upsert(ActiveResource, AccessUserName, accessToken);

        if (string.IsNullOrEmpty(refreshToken))
        {
            TryRemove(ActiveResource, RefreshUserName);
        }
        else
        {
            Upsert(ActiveResource, RefreshUserName, refreshToken);
        }

        if (accessExpiresAt is null)
        {
            TryRemove(ActiveResource, AccessExpiresUserName);
        }
        else
        {
            Upsert(ActiveResource, AccessExpiresUserName, accessExpiresAt.Value.UtcDateTime.ToString("O"));
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryGet(ActiveResource, AccessUserName));
    }

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TryGet(ActiveResource, RefreshUserName));
    }

    public Task<DateTimeOffset?> GetAccessTokenExpiresAtAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = TryGet(ActiveResource, AccessExpiresUserName);
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
        TryRemove(ActiveResource, AccessUserName);
        TryRemove(ActiveResource, RefreshUserName);
        TryRemove(ActiveResource, AccessExpiresUserName);
        return Task.CompletedTask;
    }

    public async Task<bool> HasAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(token);
    }

    public Task SetActiveAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Active slot is always Resource "SolarWin"; account id tracked by AccountSessionService.
        return Task.CompletedTask;
    }

    public async Task SaveActiveSlotToAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (accountId == Guid.Empty)
        {
            return;
        }

        var access = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(access))
        {
            return;
        }

        var refresh = await GetRefreshTokenAsync(cancellationToken).ConfigureAwait(false);
        var expires = await GetAccessTokenExpiresAtAsync(cancellationToken).ConfigureAwait(false);
        var resource = AccountResource(accountId);
        Upsert(resource, AccessUserName, access);
        if (string.IsNullOrEmpty(refresh))
        {
            TryRemove(resource, RefreshUserName);
        }
        else
        {
            Upsert(resource, RefreshUserName, refresh);
        }

        if (expires is null)
        {
            TryRemove(resource, AccessExpiresUserName);
        }
        else
        {
            Upsert(resource, AccessExpiresUserName, expires.Value.UtcDateTime.ToString("O"));
        }
    }

    public Task LoadAccountIntoActiveSlotAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resource = AccountResource(accountId);
        var access = TryGet(resource, AccessUserName);
        if (string.IsNullOrWhiteSpace(access))
        {
            // Clear active so Initialize fails cleanly.
            TryRemove(ActiveResource, AccessUserName);
            TryRemove(ActiveResource, RefreshUserName);
            TryRemove(ActiveResource, AccessExpiresUserName);
            return Task.CompletedTask;
        }

        Upsert(ActiveResource, AccessUserName, access);
        var refresh = TryGet(resource, RefreshUserName);
        if (string.IsNullOrEmpty(refresh))
        {
            TryRemove(ActiveResource, RefreshUserName);
        }
        else
        {
            Upsert(ActiveResource, RefreshUserName, refresh);
        }

        var exp = TryGet(resource, AccessExpiresUserName);
        if (string.IsNullOrEmpty(exp))
        {
            TryRemove(ActiveResource, AccessExpiresUserName);
        }
        else
        {
            Upsert(ActiveResource, AccessExpiresUserName, exp);
        }

        return Task.CompletedTask;
    }

    public Task ClearAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resource = AccountResource(accountId);
        TryRemove(resource, AccessUserName);
        TryRemove(resource, RefreshUserName);
        TryRemove(resource, AccessExpiresUserName);
        return Task.CompletedTask;
    }

    private static string AccountResource(Guid accountId) => $"SolarWin:{accountId:D}";

    private void Upsert(string resource, string userName, string secret)
    {
        TryRemove(resource, userName);
        _vault.Add(new PasswordCredential(resource, userName, secret));
    }

    private string? TryGet(string resource, string userName)
    {
        try
        {
            var credential = _vault.Retrieve(resource, userName);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void TryRemove(string resource, string userName)
    {
        try
        {
            var credential = _vault.Retrieve(resource, userName);
            _vault.Remove(credential);
        }
        catch (Exception)
        {
            // Already absent.
        }
    }
}
