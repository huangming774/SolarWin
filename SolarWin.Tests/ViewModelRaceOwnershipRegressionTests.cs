using SolarWin.Helpers;

namespace SolarWin.Tests;

public sealed class ViewModelRaceOwnershipRegressionTests
{
    [Fact]
    public void OrderPreviewCannotCommitAfterDraftChanges()
    {
        Assert.False(ViewModelRaceOwnership.OwnsOrderDraft(4, 5, "shop-a|sku-a", "shop-b|sku-b"));
        Assert.False(ViewModelRaceOwnership.OwnsOrderDraft(4, 4, "shop-a|sku-a", "shop-b|sku-b"));
        Assert.True(ViewModelRaceOwnership.OwnsOrderDraft(4, 4, "shop-a|sku-a", "shop-a|sku-a"));
    }

    [Fact]
    public void WalletPageCannotCommitAfterWalletOrFilterChanges()
    {
        var wallet = Guid.NewGuid();
        Assert.False(ViewModelRaceOwnership.OwnsWalletPage(2, 3, wallet, wallet, 20, 0, null, "income"));
        Assert.False(ViewModelRaceOwnership.OwnsWalletPage(2, 2, wallet, Guid.NewGuid(), 20, 20, null, null));
        Assert.False(ViewModelRaceOwnership.OwnsWalletPage(2, 2, wallet, wallet, 20, 0, null, null));
        Assert.True(ViewModelRaceOwnership.OwnsWalletPage(2, 2, wallet, wallet, 20, 20, null, null));
    }

    [Fact]
    public void CancelledConversationCannotMutateNewChat()
    {
        Assert.False(ViewModelRaceOwnership.OwnsConversation(7, 8));
        Assert.True(ViewModelRaceOwnership.OwnsConversation(8, 8));
    }
}
