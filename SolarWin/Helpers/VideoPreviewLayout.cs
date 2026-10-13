namespace SolarWin.Helpers;

internal readonly record struct VideoPreviewDimensions(
    double AvailableWidth,
    double AvailableHeight,
    double PlayerWidth,
    double PlayerHeight,
    double CardMaxWidth,
    double CardMaxHeight);

internal static class VideoPreviewLayout
{
    internal const double OverlayMargin = 24;
    internal const double CardPadding = 24;
    internal const double DialogChromeHeight = 160;
    internal const double MaxPlayerWidth = 1200;
    internal const double MaxPlayerHeight = 760;

    internal static VideoPreviewDimensions Calculate(
        double rootWidth,
        double rootHeight,
        double aspectRatio)
    {
        var availableWidth = Math.Clamp(
            rootWidth - ((OverlayMargin + CardPadding) * 2),
            280,
            MaxPlayerWidth);
        var availableHeight = Math.Clamp(
            rootHeight - ((OverlayMargin * 2) + DialogChromeHeight),
            180,
            MaxPlayerHeight);
        var playerWidth = availableWidth;
        var playerHeight = playerWidth / Math.Max(0.01, aspectRatio);
        if (playerHeight > availableHeight)
        {
            playerHeight = availableHeight;
            playerWidth = playerHeight * aspectRatio;
        }

        return new VideoPreviewDimensions(
            availableWidth,
            availableHeight,
            Math.Max(280, playerWidth),
            Math.Max(180, playerHeight),
            Math.Max(280, availableWidth + (CardPadding * 2)),
            Math.Max(300, availableHeight + DialogChromeHeight));
    }
}
