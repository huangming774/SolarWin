using System.Text.Json;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.Tests;

public sealed class QrLoginTests
{
    [Fact]
    public void GenerationAndPollingUseTheSameStargateOrigin()
    {
        var origin = new Uri("https://api.solian.app/");
        var id = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        Assert.Equal("https://api.solian.app/stargate/auth/qr/generate", new Uri(origin, QrLoginRoutes.Generate).AbsoluteUri);
        Assert.Equal("https://api.solian.app/stargate/auth/qr/12345678-1234-1234-1234-123456789abc",
            new Uri(origin, QrLoginRoutes.Status(id)).AbsoluteUri);
    }

    [Fact]
    public void EmptyChallengeCannotBePolled()
        => Assert.Throws<ArgumentException>(() => QrLoginRoutes.Status(Guid.Empty));

    [Theory]
    [InlineData("0", false, false, false)]
    [InlineData("1", false, false, false)]
    [InlineData("2", true, false, false)]
    [InlineData("3", false, true, false)]
    [InlineData("4", false, false, true)]
    [InlineData("\"Approved\"", true, false, false)]
    public void StatusResponsePreservesServerState(string status, bool approved, bool declined, bool expired)
    {
        var response = JsonSerializer.Deserialize<QrStatusResponse>("{\"status\":" + status + "}", JsonDefaults.Options)!;
        Assert.Equal(approved, response.IsApproved);
        Assert.Equal(declined, response.IsDeclined);
        Assert.Equal(expired, response.IsExpired);
    }
}
