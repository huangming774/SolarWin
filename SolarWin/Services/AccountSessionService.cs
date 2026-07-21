using System.Text.Json;
using SolarWin.Helpers;
using SolarWin.Models;

// JsonSerializer used for accounts.json persistence.

namespace SolarWin.Services;

public sealed class AccountSessionService : IAccountSessionService
{
    private const string ActiveIdKey = "ActiveAccountId";
    private readonly ITokenStorage _tokens;
    private readonly object _sync = new();
    private List<SavedAccountProfile> _profiles = [];

    public AccountSessionService(ITokenStorage tokens)
    {
        _tokens = tokens;
        LoadProfiles();
        if (Guid.TryParse(SettingsStore.GetString(ActiveIdKey), out var id))
        {
            ActiveAccountId = id;
        }
    }

    public Guid? ActiveAccountId { get; private set; }

    public IReadOnlyList<SavedAccountProfile> GetProfiles()
    {
        lock (_sync)
        {
            return _profiles.OrderByDescending(p => p.LastUsedAt).ToList();
        }
    }

    public async Task RememberProfileAsync(SnAccount account, CancellationToken cancellationToken = default)
    {
        if (account.Id == Guid.Empty)
        {
            return;
        }

        // Move current active tokens into per-account vault slot.
        if (_tokens is IMultiAccountTokenStorage multi)
        {
            await multi.SaveActiveSlotToAccountAsync(account.Id, cancellationToken).ConfigureAwait(false);
            await multi.SetActiveAccountAsync(account.Id, cancellationToken).ConfigureAwait(false);
        }

        ActiveAccountId = account.Id;
        SettingsStore.SetString(ActiveIdKey, account.Id.ToString("D"));

        lock (_sync)
        {
            var existing = _profiles.FirstOrDefault(p => p.AccountId == account.Id);
            if (existing is null)
            {
                existing = new SavedAccountProfile { AccountId = account.Id };
                _profiles.Add(existing);
            }

            existing.Name = account.Name;
            existing.Nick = account.Nick;
            existing.LastUsedAt = DateTimeOffset.UtcNow;
            PersistProfiles_NoLock();
        }

        // Offline cache me
        OfflineCache.SetJson($"account_me_{account.Id:N}", account, TimeSpan.FromDays(14));
    }

    public async Task SwitchToAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("accountId invalid", nameof(accountId));
        }

        if (_tokens is not IMultiAccountTokenStorage multi)
        {
            throw new InvalidOperationException("Token storage does not support multi-account.");
        }

        // Snapshot current active into its slot
        if (ActiveAccountId is { } cur && cur != Guid.Empty)
        {
            await multi.SaveActiveSlotToAccountAsync(cur, cancellationToken).ConfigureAwait(false);
        }

        await multi.LoadAccountIntoActiveSlotAsync(accountId, cancellationToken).ConfigureAwait(false);
        await multi.SetActiveAccountAsync(accountId, cancellationToken).ConfigureAwait(false);
        ActiveAccountId = accountId;
        SettingsStore.SetString(ActiveIdKey, accountId.ToString("D"));

        lock (_sync)
        {
            var p = _profiles.FirstOrDefault(x => x.AccountId == accountId);
            if (p is not null)
            {
                p.LastUsedAt = DateTimeOffset.UtcNow;
                PersistProfiles_NoLock();
            }
        }
    }

    public async Task RemoveAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (_tokens is IMultiAccountTokenStorage multi)
        {
            await multi.ClearAccountAsync(accountId, cancellationToken).ConfigureAwait(false);
        }

        OfflineCache.Remove($"account_me_{accountId:N}");
        OfflineCache.Remove($"chat_rooms_{accountId:N}");

        lock (_sync)
        {
            _profiles.RemoveAll(p => p.AccountId == accountId);
            PersistProfiles_NoLock();
        }

        if (ActiveAccountId == accountId)
        {
            ActiveAccountId = null;
            SettingsStore.SetString(ActiveIdKey, string.Empty);
            await _tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task OnLoggedOutAsync(bool removeProfile, CancellationToken cancellationToken = default)
    {
        var id = ActiveAccountId;
        await _tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
        if (_tokens is IMultiAccountTokenStorage multi && id is { } aid && aid != Guid.Empty)
        {
            await multi.ClearAccountAsync(aid, cancellationToken).ConfigureAwait(false);
        }

        if (removeProfile && id is { } rid)
        {
            await RemoveAsync(rid, cancellationToken).ConfigureAwait(false);
        }

        ActiveAccountId = null;
        SettingsStore.SetString(ActiveIdKey, string.Empty);
    }

    private void LoadProfiles()
    {
        try
        {
            AppPaths.EnsureDirectories();
            if (!File.Exists(AppPaths.AccountsFilePath))
            {
                _profiles = [];
                return;
            }

            var json = File.ReadAllText(AppPaths.AccountsFilePath);
            _profiles = JsonSerializer.Deserialize<List<SavedAccountProfile>>(json, JsonDefaults.Options) ?? [];
        }
        catch
        {
            _profiles = [];
        }
    }

    private void PersistProfiles_NoLock()
    {
        try
        {
            AppPaths.EnsureDirectories();
            File.WriteAllText(
                AppPaths.AccountsFilePath,
                JsonSerializer.Serialize(_profiles, JsonDefaults.Options));
        }
        catch
        {
            // ignore
        }
    }
}
