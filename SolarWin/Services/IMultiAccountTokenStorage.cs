namespace SolarWin.Services;

/// <summary>Optional multi-slot vault extension for <see cref="ITokenStorage"/>.</summary>
public interface IMultiAccountTokenStorage
{
    Task SetActiveAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task SaveActiveSlotToAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task LoadAccountIntoActiveSlotAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task ClearAccountAsync(Guid accountId, CancellationToken cancellationToken = default);
}
