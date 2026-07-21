using Microsoft.Toolkit.Uwp.Notifications;

namespace SolarWin.Services;

/// <summary>
/// Desktop toast via Microsoft.Toolkit.Uwp.Notifications (unpackaged-friendly).
/// </summary>
public sealed class SystemNotificationService : ISystemNotificationService
{
    private static int _hooked;

    public static void EnsureComActivatorRegistered()
    {
        if (Interlocked.Exchange(ref _hooked, 1) == 1)
        {
            return;
        }

        try
        {
            ToastNotificationManagerCompat.OnActivated += OnToastActivated;
        }
        catch
        {
            // ignore
        }
    }

    private static void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        try
        {
            App.DispatcherQueue?.TryEnqueue(() =>
            {
                if (App.Window is MainWindow mw)
                {
                    mw.ShowFromTray();
                }

                var args = e.Argument ?? string.Empty;
                // ToastContentBuilder.AddArgument("launch", uri) → launch=...
                if (args.Contains("solian:", StringComparison.OrdinalIgnoreCase))
                {
                    var start = args.IndexOf("solian:", StringComparison.OrdinalIgnoreCase);
                    var uri = args[start..].Split('&', ';', ' ')[0];
                    if (App.Services.GetService(typeof(IDeepLinkService)) is IDeepLinkService deep)
                    {
                        deep.HandleUri(Uri.UnescapeDataString(uri));
                    }
                }
                else if (!string.IsNullOrEmpty(args)
                         && args.Contains("launch=", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var part in args.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (part.StartsWith("launch=", StringComparison.OrdinalIgnoreCase))
                        {
                            var val = Uri.UnescapeDataString(part["launch=".Length..]);
                            if (App.Services.GetService(typeof(IDeepLinkService)) is IDeepLinkService deep)
                            {
                                deep.HandleUri(val);
                            }
                        }
                    }
                }
            });
        }
        catch
        {
            // ignore
        }
    }

    public void Show(string title, string body, string? launchArgs = null)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        if (body.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
            || body.Contains("eyJ", StringComparison.Ordinal))
        {
            body = "操作完成";
        }

        try
        {
            EnsureComActivatorRegistered();
            var builder = new ToastContentBuilder()
                .AddText(string.IsNullOrWhiteSpace(title) ? "Solar Network" : title.Trim())
                .AddText(body.Trim());

            if (!string.IsNullOrWhiteSpace(launchArgs))
            {
                builder.AddArgument("launch", launchArgs);
            }

            builder.Show(toast =>
            {
                toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(10);
            });
        }
        catch
        {
            // Best-effort only.
        }
    }
}
