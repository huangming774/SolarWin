using SolarWin.Helpers;

namespace SolarWin.Tests;

public sealed class TrustedMediaOriginTests
{
    [Theory]
    [InlineData("https://api.solian.app/drive/files/1")]
    [InlineData("https://api.solian.app:443/drive/files/1")]
    [InlineData("HTTPS://API.SOLIAN.APP/drive/files/1")]
    public void AcceptsExactHttpsOrigin(string url)
    {
        Assert.True(TrustedMediaOrigin.IsTrusted(url));
    }

    [Theory]
    [InlineData("http://api.solian.app/drive/files/1")]
    [InlineData("https://api.solian.app:444/drive/files/1")]
    [InlineData("https://api.solian.app.evil.example/drive/files/1")]
    [InlineData("https://evilapi.solian.app/drive/files/1")]
    [InlineData("https://cdn.api.solian.app/drive/files/1")]
    [InlineData("https://user:secret@api.solian.app/drive/files/1")]
    [InlineData("https://api.solian.app@evil.example/drive/files/1")]
    public void RejectsNonExactOrigin(string url)
    {
        Assert.False(TrustedMediaOrigin.IsTrusted(url));
    }
}
