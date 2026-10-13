namespace SolarWin.Helpers;

internal static class ImagePreviewLayout
{
    internal static (double Width, double Height) Fit(
        double imageWidth, double imageHeight, double availableWidth, double availableHeight)
    {
        availableWidth = Math.Max(1, availableWidth);
        availableHeight = Math.Max(1, availableHeight);
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            imageWidth = 800;
            imageHeight = 600;
        }

        var scale = Math.Min(1, Math.Min(availableWidth / imageWidth, availableHeight / imageHeight));
        return (imageWidth * scale, imageHeight * scale);
    }

    internal static double ZoomOffset(
        double offset, double pointer, double viewport, double content, double oldZoom, double newZoom)
    {
        // Centered content has a leading inset while it is smaller than the viewport.
        var oldInset = Math.Max(0, (viewport - content * oldZoom) / 2);
        var newInset = Math.Max(0, (viewport - content * newZoom) / 2);
        var target = (offset + pointer - oldInset) * (newZoom / oldZoom) + newInset - pointer;
        return Math.Clamp(target, 0, Math.Max(0, content * newZoom - viewport));
    }
}
