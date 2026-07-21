using System.Text.Json;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Background DM / group message alerts via WebSocket + system toast / tray balloon.
/// Only other people's brand-new messages; each message id notifies at most once.
/// </summary>
public sealed class ChatMessageNotifier : IChatMessageNotifier
{
    private const int MaxTrackedIds = 800;

    private readonly IChatWebSocketService _ws;
    private readonly IAuthService _auth;
    private readonly ISystemNotificationService _system;
    private readonly ITrayService _tray;
    private readonly IToastService _inAppToast;

    private readonly object _sync = new();
    private readonly HashSet<Guid> _notifiedMessageIds = [];
    private readonly Queue<Guid> _notifiedOrder = new();
    private bool _hooked;

    public ChatMessageNotifier(
        IChatWebSocketService ws,
        IAuthService auth,
        ISystemNotificationService system,
        ITrayService tray,
        IToastService inAppToast)
    {
        _ws = ws;
        _auth = auth;
        _system = system;
        _tray = tray;
        _inAppToast = inAppToast;
    }

    public Guid? ActiveRoomId { get; set; }

    public void Start()
    {
        if (!_auth.IsAuthenticated)
        {
            return;
        }

        lock (_sync)
        {
            if (!_hooked)
            {
                _hooked = true;
                _ws.PacketReceived += OnPacket;
                _auth.AuthenticationStateChanged += OnAuthChanged;
            }
        }

        _ = EnsureConnectedAsync();
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!_hooked)
            {
                return;
            }

            _hooked = false;
            _ws.PacketReceived -= OnPacket;
            _auth.AuthenticationStateChanged -= OnAuthChanged;
        }
    }

    private void OnAuthChanged(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            // New session → allow fresh ids; drop old account noise.
            _notifiedMessageIds.Clear();
            _notifiedOrder.Clear();
        }

        if (_auth.IsAuthenticated)
        {
            _ = EnsureConnectedAsync();
        }
    }

    private async Task EnsureConnectedAsync()
    {
        try
        {
            await _ws.ConnectAsync().ConfigureAwait(false);
        }
        catch
        {
            // UI may reconnect later
        }
    }

    private void OnPacket(object? sender, ChatWsPacket packet)
    {
        // Strict: only brand-new message events (not update / delete / reaction / read).
        if (!IsNewMessageEvent(packet.Type))
        {
            return;
        }

        if (packet.Data is not { } data || data.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        SnChatMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<SnChatMessage>(data.GetRawText(), JsonDefaults.Options);
        }
        catch
        {
            return;
        }

        if (msg is null || msg.Id == Guid.Empty)
        {
            return;
        }

        // Never notify for own messages (sender_id is chat member id — use account ids).
        if (IsOwnMessage(msg, _auth.CurrentAccount?.Id))
        {
            return;
        }

        // Dedupe: same message id → at most one popup for the whole session.
        if (!TryMarkNotified(msg.Id))
        {
            return;
        }

        if (!AppSettings.ChatMessageNotifications)
        {
            return;
        }

        var roomId = msg.ChatRoomId;
        // Viewing this conversation in the foreground → no OS/in-app popup.
        if (ActiveRoomId is { } active && active != Guid.Empty && roomId == active && IsWindowForeground())
        {
            return;
        }

        var senderName = msg.Sender?.Nick
            ?? msg.Sender?.Username
            ?? msg.Sender?.Account?.Nick
            ?? msg.Sender?.Account?.Name
            ?? "新消息";

        var roomName = msg.ChatRoom?.Name;
        var title = string.IsNullOrWhiteSpace(roomName)
            ? senderName
            : $"{senderName} · {roomName}";

        var body = BuildPreview(msg);
        var launch = roomId != Guid.Empty
            ? $"solian://chat/{roomId:D}"
            : "solian://login";

        void RaiseOnce()
        {
            // Single surface only — never system + tray + in-app together.
            if (IsWindowInTray() || !IsWindowForeground())
            {
                if (AppSettings.UseSystemNotifications)
                {
                    _system.Show(title, body, launch);
                }
                else
                {
                    try
                    {
                        _tray.EnsureVisible();
                        _tray.ShowBalloon(title, body);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                return;
            }

            // Window visible but another page/room — one in-app banner only
            // (must not also fire OS toast via ToastService, or it doubles).
            _inAppToast.ShowInAppOnly($"{title}: {body}");
        }

        if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(RaiseOnce);
        }
        else
        {
            RaiseOnce();
        }
    }

    private static bool IsNewMessageEvent(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        // Explicit create/new only — updates would re-fire the same id without this gate,
        // and also avoid reaction/read noise.
        return type.Equals("messages.new", StringComparison.OrdinalIgnoreCase)
               || type.Equals("message.new", StringComparison.OrdinalIgnoreCase)
               || type.Equals("messages.created", StringComparison.OrdinalIgnoreCase)
               || type.Equals("message.created", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Own-message detection: SnChatMessage.sender_id is the member UUID, not account id.
    /// Prefer nested sender.account_id / sender.account.id (same as chat bubbles).
    /// </summary>
    internal static bool IsOwnMessage(SnChatMessage msg, Guid? currentAccountId)
    {
        if (currentAccountId is null || currentAccountId == Guid.Empty)
        {
            return false;
        }

        var me = currentAccountId.Value;

        if (msg.Sender?.AccountId is Guid aid && aid != Guid.Empty && aid == me)
        {
            return true;
        }

        if (msg.Sender?.Account?.Id is Guid nested && nested != Guid.Empty && nested == me)
        {
            return true;
        }

        // Some payloads put account id on sender_id; keep as last resort.
        if (msg.SenderId != Guid.Empty && msg.SenderId == me)
        {
            return true;
        }

        return false;
    }

    private bool TryMarkNotified(Guid messageId)
    {
        lock (_sync)
        {
            if (!_notifiedMessageIds.Add(messageId))
            {
                return false;
            }

            _notifiedOrder.Enqueue(messageId);
            while (_notifiedOrder.Count > MaxTrackedIds)
            {
                var old = _notifiedOrder.Dequeue();
                _notifiedMessageIds.Remove(old);
            }

            return true;
        }
    }

    private static string BuildPreview(SnChatMessage msg)
    {
        if (msg.IsEncrypted)
        {
            return "[加密消息]";
        }

        if (!string.IsNullOrWhiteSpace(msg.Content))
        {
            var c = msg.Content.Trim().Replace('\n', ' ');
            return c.Length <= 120 ? c : c[..120] + "…";
        }

        if (string.Equals(msg.Type, "sticker", StringComparison.OrdinalIgnoreCase))
        {
            return "[贴纸]";
        }

        if (string.Equals(msg.Type, "image", StringComparison.OrdinalIgnoreCase)
            || string.Equals(msg.Type, "media", StringComparison.OrdinalIgnoreCase))
        {
            return "[图片]";
        }

        if (msg.Attachments is { Count: > 0 })
        {
            return "[附件]";
        }

        var t = msg.Type ?? "消息";
        return t is "text" or "" ? "（新消息）" : $"[{t}]";
    }

    private static bool IsWindowInTray()
    {
        try
        {
            if (App.Window is MainWindow { } mw)
            {
                return !mw.AppWindow.IsVisible;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static bool IsWindowForeground()
    {
        try
        {
            if (App.Window is MainWindow mw)
            {
                return mw.AppWindow.IsVisible
                       && mw.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter
                       {
                           State: not Microsoft.UI.Windowing.OverlappedPresenterState.Minimized,
                       };
            }
        }
        catch
        {
            // ignore
        }

        return true;
    }
}
