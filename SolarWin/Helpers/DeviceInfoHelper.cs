using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;

namespace SolarWin.Helpers;

/// <summary>
/// Local device identifiers used by Padlock challenge requests.
/// </summary>
public static class DeviceInfoHelper
{
    private static string? _deviceId;
    private static string? _deviceName;

    public static string GetDeviceId()
    {
        if (_deviceId is not null)
        {
            return _deviceId;
        }

        try
        {
            var systemId = SystemIdentification.GetSystemIdForPublisher();
            if (systemId?.Id is not null)
            {
                var reader = Windows.Storage.Streams.DataReader.FromBuffer(systemId.Id);
                var bytes = new byte[systemId.Id.Length];
                reader.ReadBytes(bytes);
                _deviceId = Convert.ToHexString(bytes);
                return _deviceId;
            }
        }
        catch
        {
            // Fall through to a stable local fallback.
        }

        var source = Encoding.UTF8.GetBytes($"solarwin-{Environment.MachineName}-{Environment.UserName}");
        _deviceId = Convert.ToHexString(SHA256.HashData(source));
        return _deviceId;
    }

    public static string GetDeviceName()
    {
        if (_deviceName is not null)
        {
            return _deviceName;
        }

        try
        {
            var deviceInfo = new EasClientDeviceInformation();
            _deviceName = string.IsNullOrWhiteSpace(deviceInfo.FriendlyName)
                ? Environment.MachineName
                : deviceInfo.FriendlyName;
        }
        catch
        {
            _deviceName = Environment.MachineName;
        }

        return _deviceName;
    }

    public static string GetUserAgent()
    {
        var arch = RuntimeInformation.OSArchitecture.ToString();
        var os = Environment.OSVersion.VersionString;
        return $"SolarWin/1.1 (Windows; {os}; {arch})";
    }
}
