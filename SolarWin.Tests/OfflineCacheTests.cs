using SolarWin.Helpers;

namespace SolarWin.Tests;

public class OfflineCacheTests
{
    [Fact]
    public void SetGet_RoundTrip()
    {
        var key = "test_" + Guid.NewGuid().ToString("N");
        OfflineCache.SetJson(key, new { Hello = "world" }, TimeSpan.FromMinutes(5));
        Assert.True(OfflineCache.TryGetJson<Dictionary<string, string>>(key, out var dict));
        // snake_case options: Hello becomes hello depending on options — use a simple string map
        OfflineCache.Remove(key);

        OfflineCache.SetJson(key, new Dictionary<string, string> { ["a"] = "1" }, TimeSpan.FromHours(1));
        Assert.True(OfflineCache.TryGetJson<Dictionary<string, string>>(key, out var got));
        Assert.NotNull(got);
        Assert.Equal("1", got!["a"]);
        OfflineCache.Remove(key);
    }

    [Fact]
    public void Expired_NotReturnedUnlessAllowed()
    {
        var key = "exp_" + Guid.NewGuid().ToString("N");
        OfflineCache.SetJson(key, new Dictionary<string, string> { ["k"] = "v" }, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(30);
        Assert.False(OfflineCache.TryGetJson<Dictionary<string, string>>(key, out _));
        Assert.True(OfflineCache.TryGetJson<Dictionary<string, string>>(key, out var expired, allowExpired: true));
        Assert.Equal("v", expired!["k"]);
        OfflineCache.Remove(key);
    }
}
