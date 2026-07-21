using SolarWin.Helpers;

namespace SolarWin.Services;

public sealed class ToastService : IToastService
{
    private readonly ISystemNotificationService? _system;

    public ToastService(ISystemNotificationService? system = null)
    {
        _system = system;
    }

    public event EventHandler<ToastMessage>? MessageRaised;

    public void Show(string message, ToastKind kind = ToastKind.Info)
        => ShowCore(message, kind, alsoSystem: true);

    public void ShowInAppOnly(string message, ToastKind kind = ToastKind.Info)
        => ShowCore(message, kind, alsoSystem: false);

    private void ShowCore(string message, ToastKind kind, bool alsoSystem)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // Never surface tokens.
        if (message.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
            || message.Contains("eyJ", StringComparison.Ordinal))
        {
            message = kind == ToastKind.Error ? "操作失败" : "操作完成";
        }

        var text = message.Trim();
        var toast = new ToastMessage
        {
            Text = text,
            Kind = kind,
        };

        // OS notification when enabled — skip for chat DMs that already chose a single surface.
        if (alsoSystem && AppSettings.UseSystemNotifications)
        {
            var title = kind switch
            {
                ToastKind.Error => "错误",
                ToastKind.Success => "成功",
                ToastKind.Warning => "注意",
                _ => "Solar Network",
            };
            _system?.Show(title, text);
        }

        // Marshal to UI thread when possible.
        if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(() => MessageRaised?.Invoke(this, toast));
            return;
        }

        MessageRaised?.Invoke(this, toast);
    }
}
