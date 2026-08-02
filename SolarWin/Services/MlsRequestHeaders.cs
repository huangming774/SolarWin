namespace SolarWin.Services;

public static class MlsRequestHeaders
{
    public const string DeviceIdHeaderName = "X-Device-Id";

    public static async ValueTask AttachDeviceIdAsync(
        HttpRequestMessage request,
        string relativePath,
        IMlsDeviceIdProvider deviceIdProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(deviceIdProvider);

        if (!MlsApiRoutes.RequiresDeviceId(relativePath))
        {
            return;
        }

        var deviceId = await deviceIdProvider.GetDeviceIdAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Remove(DeviceIdHeaderName);
        request.Headers.TryAddWithoutValidation(DeviceIdHeaderName, deviceId);
    }
}
