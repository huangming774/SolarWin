namespace SolarWin.Services;

public interface IMlsDeviceIdProvider
{
    ValueTask<string> GetDeviceIdAsync(CancellationToken cancellationToken = default);
}
