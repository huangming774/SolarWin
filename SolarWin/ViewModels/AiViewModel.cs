using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SolarWin.Helpers;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>
/// User-configured OpenAI-compatible AI chat: no default model, SSE streaming, optional 1M context.
/// </summary>
public partial class AiViewModel : ObservableObject
{
    /// <summary>Session caps — singleton VM lives for the whole process; text is cheap but unbounded isn't.</summary>
    private const int MaxHistoryTurns = 160;
    private const int MaxBubbles = 160;

    private readonly IAiChatService _ai;
    private readonly IToastService _toast;
    private CancellationTokenSource? _sendCts;
    private readonly List<AiChatTurn> _history = [];

    public AiViewModel(IAiChatService ai, IToastService toast)
    {
        _ai = ai;
        _toast = toast;
        LoadSettingsFromStore();
    }

    public ObservableCollection<AiBubbleItem> Messages { get; } = [];

    public IReadOnlyList<string> EffortLabels { get; } =
    [
        "关闭（不传参数）",
        "低 (low)",
        "中 (medium)",
        "高 (high)",
    ];

    private static readonly string?[] EffortValues = [null, "low", "medium", "high"];

    [ObservableProperty]
    public partial string ApiUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModelName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Draft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int SelectedEffortIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Context1MLabel))]
    public partial bool Use1MContext { get; set; }

    public string Context1MLabel => Use1MContext ? "1M 上下文 · 开" : "1M 上下文";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SettingsVisibility))]
    public partial bool IsSettingsExpanded { get; set; } = true;

    public Visibility SettingsVisibility =>
        IsSettingsExpanded ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyPropertyChangedFor(nameof(CancelVisibility))]
    public partial bool IsSending { get; set; }

    public Visibility CancelVisibility =>
        IsSending ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "请配置 API URL、Key 与模型名称";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool CanSend =>
        !IsSending
        && !string.IsNullOrWhiteSpace(Draft)
        && !string.IsNullOrWhiteSpace(ApiUrl)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ModelName);

    partial void OnDraftChanged(string value) => SendCommand.NotifyCanExecuteChanged();

    partial void OnApiUrlChanged(string value) => SendCommand.NotifyCanExecuteChanged();

    partial void OnApiKeyChanged(string value) => SendCommand.NotifyCanExecuteChanged();

    partial void OnModelNameChanged(string value) => SendCommand.NotifyCanExecuteChanged();

    partial void OnSelectedEffortIndexChanged(int value)
    {
        AppSettings.AiReasoningEffort = ResolveEffort() ?? string.Empty;
    }

    partial void OnUse1MContextChanged(bool value)
    {
        AppSettings.AiUse1MContext = value;
        StatusText = value
            ? "已开启 1M 上下文（保留完整历史，适合长上下文模型）"
            : "已关闭 1M 上下文（默认会裁剪过长历史）";
    }

    public void LoadSettingsFromStore()
    {
        ApiUrl = AppSettings.AiApiUrl;
        ApiKey = AppSettings.AiApiKey;
        ModelName = AppSettings.AiModelName;
        Use1MContext = AppSettings.AiUse1MContext;

        var saved = AppSettings.AiReasoningEffort;
        SelectedEffortIndex = saved.ToLowerInvariant() switch
        {
            "low" => 1,
            "medium" => 2,
            "high" => 3,
            _ => 0,
        };

        var configured = !string.IsNullOrWhiteSpace(ApiUrl)
                         && !string.IsNullOrWhiteSpace(ApiKey)
                         && !string.IsNullOrWhiteSpace(ModelName);
        IsSettingsExpanded = !configured;
        StatusText = configured
            ? $"模型 {ModelName} · 就绪{(Use1MContext ? " · 1M 上下文" : string.Empty)} · 流式"
            : "请填写 API URL、Key 与模型名称（无默认模型）";
    }

    [RelayCommand]
    private void SaveSettings()
    {
        AppSettings.AiApiUrl = ApiUrl?.Trim() ?? string.Empty;
        AppSettings.AiApiKey = ApiKey ?? string.Empty;
        AppSettings.AiModelName = ModelName?.Trim() ?? string.Empty;
        AppSettings.AiReasoningEffort = ResolveEffort() ?? string.Empty;
        AppSettings.AiUse1MContext = Use1MContext;

        ApiUrl = AppSettings.AiApiUrl;
        ModelName = AppSettings.AiModelName;

        ErrorMessage = null;
        StatusText = string.IsNullOrWhiteSpace(ModelName)
            ? "已保存（请填写模型名称后再对话）"
            : $"已保存 · {ModelName}";
        IsSettingsExpanded = string.IsNullOrWhiteSpace(ApiUrl)
                             || string.IsNullOrWhiteSpace(ApiKey)
                             || string.IsNullOrWhiteSpace(ModelName);
        _toast.Success("AI 配置已保存");
        SendCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsExpanded = !IsSettingsExpanded;

    [RelayCommand]
    private void Toggle1MContext() => Use1MContext = !Use1MContext;

    [RelayCommand]
    private void NewChat()
    {
        _sendCts?.Cancel();
        _history.Clear();
        Messages.Clear();
        ErrorMessage = null;
        StatusText = "新对话";
    }

    [RelayCommand]
    private void CancelSend() => _sendCts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = Draft.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        AppSettings.AiApiUrl = ApiUrl.Trim();
        AppSettings.AiApiKey = ApiKey;
        AppSettings.AiModelName = ModelName.Trim();
        AppSettings.AiReasoningEffort = ResolveEffort() ?? string.Empty;
        AppSettings.AiUse1MContext = Use1MContext;

        Draft = string.Empty;
        ErrorMessage = null;
        IsSending = true;
        StatusText = Use1MContext ? "流式生成中…（1M 上下文）" : "流式生成中…";

        Messages.Add(AiBubbleItem.User(text));
        _history.Add(new AiChatTurn { Role = "user", Content = text });

        var assistantBubble = AiBubbleItem.AssistantPlaceholder(
            string.IsNullOrWhiteSpace(ModelName) ? null : ModelName.Trim());
        assistantBubble.Content = string.Empty;
        Messages.Add(assistantBubble);

        _sendCts?.Cancel();
        _sendCts = new CancellationTokenSource();
        var ct = _sendCts.Token;

        var contentBuf = new StringBuilder();
        var reasoningBuf = new StringBuilder();
        var lastUiFlush = Environment.TickCount64;

        try
        {
            var request = new AiChatRequest
            {
                ApiUrl = ApiUrl.Trim(),
                ApiKey = ApiKey,
                Model = ModelName.Trim(),
                Messages = _history,
                ReasoningEffort = ResolveEffort(),
                Use1MContext = Use1MContext,
                Stream = true,
            };

            var result = await _ai.StreamCompleteAsync(
                request,
                async delta =>
                {
                    if (!string.IsNullOrEmpty(delta.Content))
                    {
                        contentBuf.Append(delta.Content);
                    }

                    if (!string.IsNullOrEmpty(delta.Reasoning))
                    {
                        reasoningBuf.Append(delta.Reasoning);
                    }

                    // Throttle UI updates ~30ms for smooth streaming
                    var now = Environment.TickCount64;
                    if (now - lastUiFlush < 30 && contentBuf.Length > 0)
                    {
                        return;
                    }

                    lastUiFlush = now;
                    var c = contentBuf.ToString();
                    var r = reasoningBuf.ToString();
                    await App.DispatcherQueue.EnqueueAsync(() =>
                    {
                        assistantBubble.Content = c.Length > 0 ? c : "…";
                        if (r.Length > 0)
                        {
                            assistantBubble.ReasoningText = r;
                            assistantBubble.HasReasoning = true;
                        }
                    }).ConfigureAwait(false);
                },
                ct).ConfigureAwait(true);

            // Final paint
            if (contentBuf.Length > 0)
            {
                assistantBubble.Content = contentBuf.ToString();
            }

            if (reasoningBuf.Length > 0)
            {
                assistantBubble.ReasoningText = reasoningBuf.ToString();
                assistantBubble.HasReasoning = true;
            }

            if (!result.Ok)
            {
                if (contentBuf.Length == 0)
                {
                    assistantBubble.Content = result.Error ?? "请求失败";
                    assistantBubble.IsError = true;
                    assistantBubble.RoleLabel = "错误";
                }
                else
                {
                    // Partial stream then error
                    assistantBubble.Content = contentBuf + "\n\n（中断：" + (result.Error ?? "错误") + "）";
                    assistantBubble.IsError = true;
                }

                ErrorMessage = result.Error;
                StatusText = "发送失败";
                if (contentBuf.Length == 0)
                {
                    _toast.Error(result.Error ?? "AI 请求失败");
                }

                return;
            }

            var content = contentBuf.Length > 0
                ? contentBuf.ToString()
                : (result.Content ?? string.Empty);
            if (string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(result.Content))
            {
                content = result.Content!;
                assistantBubble.Content = content;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                content = "(无文本回复)";
                assistantBubble.Content = content;
            }

            assistantBubble.IsError = false;
            assistantBubble.RoleLabel = "助手";
            assistantBubble.TimeText = DateTime.Now.ToString("HH:mm");
            _history.Add(new AiChatTurn { Role = "assistant", Content = content });

            var mode = result.UsedStreaming ? "流式" : "整包";
            var effort = ResolveEffort();
            StatusText = effort is null
                ? $"完成 · {ModelName.Trim()} · {mode}{(Use1MContext ? " · 1M" : string.Empty)}"
                : $"完成 · {ModelName.Trim()} · 推理 {effort} · {mode}{(Use1MContext ? " · 1M" : string.Empty)}";
        }
        catch (OperationCanceledException)
        {
            if (contentBuf.Length > 0)
            {
                assistantBubble.Content = contentBuf + "\n\n（已停止）";
                _history.Add(new AiChatTurn { Role = "assistant", Content = contentBuf.ToString() });
            }
            else
            {
                assistantBubble.Content = "（已取消）";
            }

            assistantBubble.IsError = true;
            assistantBubble.RoleLabel = "取消";
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            assistantBubble.Content = contentBuf.Length > 0
                ? contentBuf + "\n\n（错误：" + ex.Message + "）"
                : ex.Message;
            assistantBubble.IsError = true;
            assistantBubble.RoleLabel = "错误";
            ErrorMessage = ex.Message;
            StatusText = "发送失败";
            _toast.Error(ex.Message);
        }
        finally
        {
            IsSending = false;
            TrimHistory();
            SendCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Drop oldest turns/bubbles beyond the session cap (never mid-stream).</summary>
    private void TrimHistory()
    {
        if (_history.Count > MaxHistoryTurns)
        {
            _history.RemoveRange(0, _history.Count - MaxHistoryTurns);
        }

        while (Messages.Count > MaxBubbles)
        {
            Messages.RemoveAt(0);
        }
    }

    private string? ResolveEffort()
    {
        if (SelectedEffortIndex < 0 || SelectedEffortIndex >= EffortValues.Length)
        {
            return null;
        }

        return EffortValues[SelectedEffortIndex];
    }
}

/// <summary>Chat bubble for the AI page ListView.</summary>
public partial class AiBubbleItem : ObservableObject
{
    [ObservableProperty]
    public partial string RoleLabel { get; set; } = "助手";

    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TimeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ModelHint { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReasoningVisibility))]
    public partial string? ReasoningText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReasoningVisibility))]
    public partial bool HasReasoning { get; set; }

    public Visibility ReasoningVisibility =>
        HasReasoning && !string.IsNullOrWhiteSpace(ReasoningText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    public bool IsUser { get; init; }

    public HorizontalAlignment Alignment =>
        IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public static AiBubbleItem User(string content) => new()
    {
        IsUser = true,
        RoleLabel = "我",
        Content = content,
        TimeText = DateTime.Now.ToString("HH:mm"),
    };

    public static AiBubbleItem AssistantPlaceholder(string? model) => new()
    {
        IsUser = false,
        RoleLabel = "助手",
        Content = string.Empty,
        ModelHint = model,
        TimeText = DateTime.Now.ToString("HH:mm"),
    };
}

/// <summary>DispatcherQueue helpers for streaming UI updates.</summary>
internal static class DispatcherQueueExtensions
{
    public static Task EnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue queue, Action action)
    {
        var tcs = new TaskCompletionSource();
        if (!queue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }

        return tcs.Task;
    }
}
