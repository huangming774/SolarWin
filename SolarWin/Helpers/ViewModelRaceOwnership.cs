namespace SolarWin.Helpers;

/// <summary>Pure latest-owner checks shared by async ViewModels and regression tests.</summary>
public static class ViewModelRaceOwnership
{
    public static bool OwnsOrderDraft(
        int snapshotGeneration,
        int currentGeneration,
        string? snapshotSignature,
        string? currentSignature)
        => snapshotGeneration == currentGeneration
           && !string.IsNullOrWhiteSpace(snapshotSignature)
           && string.Equals(snapshotSignature, currentSignature, StringComparison.Ordinal);

    public static bool OwnsWalletPage(
        int snapshotVersion,
        int currentVersion,
        Guid snapshotWalletId,
        Guid? currentWalletId,
        int snapshotOffset,
        int currentOffset,
        string? snapshotDirection,
        string? currentDirection)
        => snapshotVersion == currentVersion
           && snapshotWalletId == currentWalletId
           && snapshotOffset == currentOffset
           && string.Equals(snapshotDirection, currentDirection, StringComparison.Ordinal);

    public static bool OwnsConversation(int snapshotGeneration, int currentGeneration)
        => snapshotGeneration == currentGeneration;
}
