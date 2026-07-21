using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>
/// 寻思 (Personality / Thoughts): pick bot + model, then chat.
/// API base: /personality (agents, conversations, messages, runs + SSE).
/// </summary>
public partial class ThinkingViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;

    private string? _conversationId;
    private List<ThoughtAgent> _agents = [];

    public ThinkingViewModel(ISolarApiClient api, IToastService toast)
    {
        _api = api;
        _toast = toast;
    }

    public ObservableCollection<string> AgentNames { get; } = [];
    public ObservableCollection<string> ModelNames { get; } = [];
    public ObservableCollection<ThoughtBubbleItem> Messages { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "选择机器人与模型，开始寻思";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Draft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int SelectedAgentIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int SelectedModelIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string ConversationTitle { get; set; } = "新对话";

    public bool CanSend =>
        !IsSending
        && !string.IsNullOrWhiteSpace(Draft)
        && SelectedAgentIndex >= 0
        && _agents.Count > 0;

    partial void OnSelectedAgentIndexChanged(int value)
    {
        RebuildModelList();
        // Changing bot starts a new conversation (Solian behavior).
        if (!string.IsNullOrWhiteSpace(_conversationId))
        {
            _conversationId = null;
            Messages.Clear();
            ConversationTitle = "新对话";
        }

        OnPropertyChanged(nameof(CanSend));
        SendCommand.NotifyCanExecuteChanged();
    }

    partial void OnDraftChanged(string value)
    {
        OnPropertyChanged(nameof(CanSend));
        SendCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            StatusText = "加载机器人列表…";

            _agents = await _api.GetThoughtAgentsAsync().ConfigureAwait(true);
            AgentNames.Clear();
            foreach (var a in _agents)
            {
                AgentNames.Add(a.DisplayName);
            }

            if (_agents.Count == 0)
            {
                StatusText = "暂无可用机器人";
                ErrorMessage = "GET /personality/agents 返回空列表";
                SelectedAgentIndex = -1;
                return;
            }

            SelectedAgentIndex = 0;
            RebuildModelList();
            StatusText = "选择机器人与模型后，在下方输入并发送";
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.ApiMessage ?? ex.Message;
            StatusText = "加载失败";
            _toast.Error(ErrorMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "加载失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildModelList()
    {
        ModelNames.Clear();
        ModelNames.Add("自动 (Auto)");
        if (SelectedAgentIndex < 0 || SelectedAgentIndex >= _agents.Count)
        {
            SelectedModelIndex = 0;
            return;
        }

        foreach (var m in _agents[SelectedAgentIndex].Models)
        {
            ModelNames.Add(m.Label);
        }

        SelectedModelIndex = 0;
    }

    private string? ResolveSelectedAgentId()
    {
        if (SelectedAgentIndex < 0 || SelectedAgentIndex >= _agents.Count)
        {
            return null;
        }

        return _agents[SelectedAgentIndex].Id;
    }

    private string? ResolveSelectedModelId()
    {
        if (SelectedModelIndex <= 0)
        {
            return null;
        }

        if (SelectedAgentIndex < 0 || SelectedAgentIndex >= _agents.Count)
        {
            return null;
        }

        var models = _agents[SelectedAgentIndex].Models;
        var idx = SelectedModelIndex - 1;
        if (idx < 0 || idx >= models.Count)
        {
            return null;
        }

        return models[idx].Id;
    }

    [RelayCommand]
    private void NewConversation()
    {
        _conversationId = null;
        Messages.Clear();
        ConversationTitle = "新对话";
        Draft = string.Empty;
        StatusText = "新对话 — 发送消息后将创建会话";
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = Draft?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return;
        }

        var agentId = ResolveSelectedAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            _toast.Show("请先选择机器人");
            return;
        }

        try
        {
            IsSending = true;
            ErrorMessage = null;
            StatusText = "发送中…";

            Messages.Add(new ThoughtBubbleItem
            {
                Role = "user",
                Content = text,
                TimeText = DateTimeOffset.Now.ToString("HH:mm"),
            });
            Draft = string.Empty;

            // Ensure conversation (only create once)
            if (string.IsNullOrWhiteSpace(_conversationId))
            {
                StatusText = "创建会话…";
                var title = text.Split('\n')[0];
                if (title.Length > 48)
                {
                    title = title[..48];
                }

                var conv = await _api.CreateThoughtConversationAsync(new CreateThoughtConversationRequest
                {
                    AgentId = agentId,
                    Title = title,
                }).ConfigureAwait(true);
                _conversationId = conv.Id;
                ConversationTitle = conv.DisplayTitle;
            }

            // Solian official: only POST /runs with message (do NOT also POST /messages first —
            // that can confuse some agents / duplicate user turns).
            StatusText = "机器人思考中…";
            var run = await _api.RunThoughtAsync(_conversationId!, new ThoughtRunRequest
            {
                Message = text,
                Stream = true,
                Model = ResolveSelectedModelId(),
            }).ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(run.ConversationId))
            {
                _conversationId = run.ConversationId;
            }

            if (!string.IsNullOrWhiteSpace(run.ConversationTitle))
            {
                ConversationTitle = run.ConversationTitle!;
            }

            var reply = run.Content?.Trim() ?? string.Empty;

            // If stream/json returned empty, reload server messages (assistant may already be stored).
            if (string.IsNullOrWhiteSpace(reply))
            {
                reply = await ReloadAndFindAssistantReplyAsync(text).ConfigureAwait(true);
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                // One short poll: run may finish async after headers
                for (var i = 0; i < 4 && string.IsNullOrWhiteSpace(reply); i++)
                {
                    await Task.Delay(800).ConfigureAwait(true);
                    reply = await ReloadAndFindAssistantReplyAsync(text).ConfigureAwait(true);
                }
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                Messages.Add(new ThoughtBubbleItem
                {
                    Role = "assistant",
                    Content = "（未收到回复。请确认机器人可用，或点「历史」重新加载。）",
                    TimeText = DateTimeOffset.Now.ToString("HH:mm"),
                    IsError = true,
                });
                StatusText = "未收到回复";
                return;
            }

            Messages.Add(new ThoughtBubbleItem
            {
                Role = "assistant",
                Content = reply,
                TimeText = DateTimeOffset.Now.ToString("HH:mm"),
                ModelHint = run.Model ?? ResolveSelectedModelId(),
            });
            StatusText = "就绪";
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.ApiMessage ?? ex.Message;
            StatusText = "发送失败";
            Messages.Add(new ThoughtBubbleItem
            {
                Role = "assistant",
                Content = "错误：" + ErrorMessage,
                TimeText = DateTimeOffset.Now.ToString("HH:mm"),
                IsError = true,
            });
            _toast.Error(ErrorMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "发送失败";
            _toast.Error(ex.Message);
        }
        finally
        {
            IsSending = false;
        }
    }

    /// <summary>
    /// Reload conversation messages and pick the latest assistant reply
    /// (optionally after the given user text).
    /// </summary>
    private async Task<string> ReloadAndFindAssistantReplyAsync(string userText)
    {
        if (string.IsNullOrWhiteSpace(_conversationId))
        {
            return string.Empty;
        }

        try
        {
            var msgs = await _api.GetThoughtMessagesAsync(_conversationId).ConfigureAwait(true);
            if (msgs.Count == 0)
            {
                return string.Empty;
            }

            // Prefer last assistant message after last matching user message
            var lastUserIdx = -1;
            for (var i = msgs.Count - 1; i >= 0; i--)
            {
                if (msgs[i].IsUser
                    && string.Equals(msgs[i].Content?.Trim(), userText.Trim(), StringComparison.Ordinal))
                {
                    lastUserIdx = i;
                    break;
                }
            }

            for (var i = msgs.Count - 1; i > lastUserIdx; i--)
            {
                if (msgs[i].IsAssistant && !string.IsNullOrWhiteSpace(msgs[i].Content))
                {
                    // Keep UI in sync with server order (user bubbles already shown optimistically)
                    return msgs[i].Content!.Trim();
                }
            }

            // Any last assistant
            var lastAssist = msgs.LastOrDefault(m => m.IsAssistant && !string.IsNullOrWhiteSpace(m.Content));
            return lastAssist?.Content?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        try
        {
            IsBusy = true;
            var list = await _api.GetThoughtConversationsAsync(take: 10).ConfigureAwait(true);
            if (list.Count == 0)
            {
                _toast.Show("没有历史对话");
                return;
            }

            var latest = list.OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt).First();
            _conversationId = latest.Id;
            ConversationTitle = latest.DisplayTitle;

            if (!string.IsNullOrWhiteSpace(latest.AgentId))
            {
                var idx = _agents.FindIndex(a =>
                    string.Equals(a.Id, latest.AgentId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    SelectedAgentIndex = idx;
                }
            }

            var msgs = await _api.GetThoughtMessagesAsync(latest.Id).ConfigureAwait(true);
            Messages.Clear();
            foreach (var m in msgs)
            {
                Messages.Add(ThoughtBubbleItem.FromMessage(m));
            }

            StatusText = $"已加载：{ConversationTitle}";
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.ApiMessage ?? ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class ThoughtBubbleItem
{
    public string Role { get; init; } = "user";

    public string Content { get; init; } = string.Empty;

    public string TimeText { get; init; } = string.Empty;

    public string? ModelHint { get; init; }

    public bool IsError { get; init; }

    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);

    public string RoleLabel => IsUser ? "我" : "机器人";

    public Microsoft.UI.Xaml.HorizontalAlignment Alignment =>
        IsUser ? Microsoft.UI.Xaml.HorizontalAlignment.Right : Microsoft.UI.Xaml.HorizontalAlignment.Left;

    public static ThoughtBubbleItem FromMessage(ThoughtMessage m)
        => new()
        {
            Role = m.Role ?? "assistant",
            Content = m.Content ?? string.Empty,
            TimeText = m.CreatedAt?.ToLocalTime().ToString("HH:mm") ?? string.Empty,
        };
}
