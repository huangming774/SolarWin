using SolarWin.Helpers;

namespace SolarWin.Services;

public interface IDeepLinkService
{
    /// <summary>Register solian:// for unpackaged installs (HKCU). Packaged uses Appxmanifest.</summary>
    void EnsureProtocolRegistered();

    /// <summary>Handle activation URI (solian:// or https://solian.app/...).</summary>
    DeepLinkAction HandleUri(string? uri);

    event EventHandler<DeepLinkAction>? DeepLinkReceived;
}
