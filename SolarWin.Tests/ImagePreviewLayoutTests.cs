using SolarWin.Helpers;

namespace SolarWin.Tests;

public class ImagePreviewLayoutTests
{
    [Theory]
    [InlineData(2400, 1200, 1000, 600, 1000, 500)]
    [InlineData(1200, 2400, 1000, 600, 300, 600)]
    [InlineData(100, 80, 1000, 600, 100, 80)]
    [InlineData(2400, 1200, 320, 240, 320, 160)]
    [InlineData(0, 0, 400, 400, 400, 300)]
    [InlineData(800, 600, 0, 0, 1, 0.75)]
    public void FitPreservesAspectRatioAndStaysWithinViewport(
        double width, double height, double availableWidth, double availableHeight,
        double expectedWidth, double expectedHeight)
    {
        var size = ImagePreviewLayout.Fit(width, height, availableWidth, availableHeight);

        Assert.Equal(expectedWidth, size.Width, 6);
        Assert.Equal(expectedHeight, size.Height, 6);
    }

    [Theory]
    [InlineData(0, 200, 400, 400, 1, 2, 200)]
    [InlineData(0, 200, 400, 400, 0.5, 2, 200)]
    [InlineData(0, 150, 400, 400, 0.5, 2, 50)]
    [InlineData(100, 100, 400, 800, 1, 2, 300)]
    [InlineData(200, 200, 400, 400, 2, 0.5, 0)]
    [InlineData(0, 0, 400, 400, 0.5, 2, 0)]
    [InlineData(0, 400, 400, 400, 0.5, 2, 400)]
    public void ZoomKeepsPointerAnchoredAndClampsToScrollableBounds(
        double offset, double pointer, double viewport, double content,
        double oldZoom, double newZoom, double expected)
    {
        Assert.Equal(expected, ImagePreviewLayout.ZoomOffset(
            offset, pointer, viewport, content, oldZoom, newZoom), 6);
    }
}
