using SolarWin.Helpers;

namespace SolarWin.Tests;

public sealed class VideoPreviewLayoutTests
{
    [Fact]
    public void LandscapeVideoFitsWidthAndHeightBounds()
    {
        var layout = VideoPreviewLayout.Calculate(1600, 1000, 16d / 9d);

        Assert.Equal(1200, layout.AvailableWidth);
        Assert.Equal(760, layout.AvailableHeight);
        Assert.Equal(1200, layout.PlayerWidth);
        Assert.Equal(675, layout.PlayerHeight);
        Assert.Equal(1248, layout.CardMaxWidth);
        Assert.Equal(920, layout.CardMaxHeight);
    }

    [Fact]
    public void ResizeRecomputesCardMaximums()
    {
        var layout = VideoPreviewLayout.Calculate(2200, 1600, 16d / 9d);

        Assert.Equal(1248, layout.CardMaxWidth);
        Assert.Equal(920, layout.CardMaxHeight);
        Assert.True(layout.PlayerWidth <= layout.AvailableWidth);
        Assert.True(layout.PlayerHeight <= layout.AvailableHeight);
    }

    [Fact]
    public void PortraitVideoUsesHeightConstraint()
    {
        var layout = VideoPreviewLayout.Calculate(1000, 760, 9d / 16d);

        Assert.Equal(552, layout.PlayerHeight);
        Assert.Equal(310.5, layout.PlayerWidth, precision: 6);
    }
}
