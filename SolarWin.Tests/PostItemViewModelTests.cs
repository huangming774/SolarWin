using SolarWin.ViewModels;

namespace SolarWin.Tests;

/// <summary>
/// Pure-logic checks for post item handle validation.
/// Full PostItemViewModel construction needs WinUI / CommunityToolkit and is not linked here.
/// </summary>
public class PostItemViewModelHandleTests
{
    [Theory]
    [InlineData("alice", true)]
    [InlineData("alice_01", true)]
    [InlineData("user.name", true)]
    [InlineData("清沫", false)]
    [InlineData("a b", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LooksLikeAccountHandle_ValidatesSlug(string? value, bool expected)
        => Assert.Equal(expected, PostItemHandleRules.LooksLikeAccountHandle(value));
}
