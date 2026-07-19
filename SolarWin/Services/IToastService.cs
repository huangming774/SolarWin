namespace SolarWin.Services;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>In-app toast / banner for success and failure feedback.</summary>
public interface IToastService
{
    event EventHandler<ToastMessage>? MessageRaised;

    void Show(string message, ToastKind kind = ToastKind.Info);

    void Success(string message) => Show(message, ToastKind.Success);

    void Error(string message) => Show(message, ToastKind.Error);

    void Warning(string message) => Show(message, ToastKind.Warning);
}

public sealed class ToastMessage
{
    public required string Text { get; init; }

    public ToastKind Kind { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
}
