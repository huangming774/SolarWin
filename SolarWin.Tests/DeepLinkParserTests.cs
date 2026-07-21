using SolarWin.Helpers;

namespace SolarWin.Tests;

public class DeepLinkParserTests
{
    [Theory]
    [InlineData("solian://user/alice", DeepLinkKind.UserProfile, "alice")]
    [InlineData("solian://auth/qr/9aaafe46-77e6-4ac2-8bca-b53b9a7c0abb", DeepLinkKind.QrLogin, "9aaafe46-77e6-4ac2-8bca-b53b9a7c0abb")]
    [InlineData("solian://chat/11111111-1111-1111-1111-111111111111", DeepLinkKind.ChatRoom, "11111111-1111-1111-1111-111111111111")]
    [InlineData("solian://post/abc", DeepLinkKind.Post, "abc")]
    [InlineData("solian://settings", DeepLinkKind.Settings, null)]
    [InlineData("https://solian.app/@bob", DeepLinkKind.UserProfile, "bob")]
    public void Parse_KnownSchemes(string uri, DeepLinkKind kind, string? value)
    {
        var action = DeepLinkParser.Parse(uri);
        Assert.Equal(kind, action.Kind);
        Assert.Equal(value, action.Value);
    }

    [Fact]
    public void IsSolarUri_DetectsProtocol()
    {
        Assert.True(DeepLinkParser.IsSolarUri("solian://login"));
        Assert.True(DeepLinkParser.IsSolarUri("https://solian.app/foo"));
        Assert.False(DeepLinkParser.IsSolarUri("https://example.com"));
        Assert.False(DeepLinkParser.IsSolarUri(null));
    }
}
