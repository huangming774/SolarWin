using Microsoft.Win32;
using SolarWin.Helpers;

namespace SolarWin.Services;

public sealed class DeepLinkService : IDeepLinkService
{
    public event EventHandler<DeepLinkAction>? DeepLinkReceived;

    public void EnsureProtocolRegistered()
    {
        try
        {
            // Unpackaged: register HKCU protocol handler pointing at this exe.
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            {
                return;
            }

            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{DeepLinkParser.ProtocolScheme}");
            key?.SetValue(string.Empty, "URL:Solar Network Protocol");
            key?.SetValue("URL Protocol", string.Empty);
            using var icon = key?.CreateSubKey("DefaultIcon");
            icon?.SetValue(string.Empty, $"\"{exe}\",0");
            using var cmd = key?.CreateSubKey(@"shell\open\command");
            cmd?.SetValue(string.Empty, $"\"{exe}\" \"%1\"");
        }
        catch
        {
            // Non-fatal (no admin rights etc.)
        }
    }

    public DeepLinkAction HandleUri(string? uri)
    {
        var action = DeepLinkParser.Parse(uri);
        try
        {
            DeepLinkReceived?.Invoke(this, action);
        }
        catch
        {
            // listeners must not break activation
        }

        return action;
    }
}
