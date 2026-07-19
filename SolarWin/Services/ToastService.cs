namespace SolarWin.Services;

public sealed class ToastService : IToastService
{
    public event EventHandler<ToastMessage>? MessageRaised;

    public void Show(string message, ToastKind kind = ToastKind.Info)
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

        var toast = new ToastMessage
        {
            Text = message.Trim(),
            Kind = kind,
        };

        // Marshal to UI thread when possible.
        if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(() => MessageRaised?.Invoke(this, toast));
            return;
        }

        MessageRaised?.Invoke(this, toast);
    }
}
