using System.Text;
using SolarWin.Helpers;

namespace SolarWin.Services;

/// <summary>
/// Persists the public Padlock device identifier once so MLS requests use the
/// same X-Device-Id after process restarts. This file contains no key material.
/// </summary>
public sealed class PersistentMlsDeviceIdProvider : IMlsDeviceIdProvider
{
    private readonly string _path;
    private readonly Func<string> _seedFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cached;

    public PersistentMlsDeviceIdProvider()
        : this(Path.Combine(AppPaths.RootDirectory, "device-id"), DeviceInfoHelper.GetDeviceId)
    {
    }

    public PersistentMlsDeviceIdProvider(string path, Func<string>? seedFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _seedFactory = seedFactory ?? DeviceInfoHelper.GetDeviceId;
    }

    public async ValueTask<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            if (File.Exists(_path))
            {
                _cached = Validate(await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false));
                return _cached;
            }

            var value = Validate(_seedFactory());
            var directory = Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("The MLS device-id path has no parent directory.");
            Directory.CreateDirectory(directory);

            var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(temporaryPath, value, new UTF8Encoding(false), cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    File.Move(temporaryPath, _path);
                }
                catch (IOException) when (File.Exists(_path))
                {
                    value = Validate(await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false));
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            _cached = value;
            return value;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string Validate(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 1024)
        {
            throw new InvalidDataException("The persisted MLS device identifier is invalid.");
        }

        return normalized;
    }
}
