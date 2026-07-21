using SolarWin.Helpers;

namespace SolarWin.Tests;

public class ApiErrorParserTests
{
    [Fact]
    public void TryGetMessage_PadlockStyle()
    {
        var msg = ApiErrorParser.TryGetMessage(
            """{"code":"PADLOCK_CAPTCHA_INVALID","message":"Invalid captcha.","status":400}""");
        Assert.NotNull(msg);
        Assert.Contains("Invalid captcha", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PADLOCK_CAPTCHA_INVALID", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetMessage_OAuthStyle()
    {
        var msg = ApiErrorParser.TryGetMessage(
            """{"error":"invalid_grant","error_description":"code expired"}""");
        Assert.Equal("code expired", msg);
    }

    [Fact]
    public void TryGetMessage_Empty()
    {
        Assert.Null(ApiErrorParser.TryGetMessage(null));
        Assert.Null(ApiErrorParser.TryGetMessage(""));
        // Non-JSON may fall through to raw body depending on parser implementation.
        var nonJson = ApiErrorParser.TryGetMessage("not-json");
        Assert.True(nonJson is null || nonJson.Contains("not-json", StringComparison.Ordinal));
    }
}
