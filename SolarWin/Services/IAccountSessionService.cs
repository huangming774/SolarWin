using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>Multi-account profiles + switch active PasswordVault token slot.</summary>
public interface IAccountSessionService
{
    Guid? ActiveAccountId { get; }

    IReadOnlyList<SavedAccountProfile> GetProfiles();

    /// <summary>Remember profile metadata after successful login/me.</summary>
    Task RememberProfileAsync(SnAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switch active account: persist current tokens under previous id, load target tokens into active slot.
    /// Caller should re-run AuthService.InitializeAsync afterwards.
    /// </summary>
    Task SwitchToAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Remove a saved profile and its vault tokens (not the active session unless it matches).</summary>
    Task RemoveAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>After logout: clear active tokens but keep profile list unless <paramref name="removeProfile"/>.</summary>
    Task OnLoggedOutAsync(bool removeProfile, CancellationToken cancellationToken = default);
}
