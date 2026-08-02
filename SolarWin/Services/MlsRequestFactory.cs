using SolarWin.Models;

namespace SolarWin.Services;

public static class MlsRequestFactory
{
    public static PublishMlsKeyPackageBody BindDeviceId(
        PublishMlsKeyPackageBody request,
        string deviceId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        return new PublishMlsKeyPackageBody
        {
            KeyPackage = request.KeyPackage,
            Ciphersuite = request.Ciphersuite,
            DeviceId = deviceId.Trim(),
            DeviceLabel = request.DeviceLabel,
            Meta = request.Meta,
        };
    }
}
