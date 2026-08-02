using System.Text.Json;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SolarWin.Helpers;

namespace SolarWin.Services;

/// <summary>
/// Listens for call-related WebSocket packets and shows a ringing incoming-call state.
/// Packet type matching is intentionally loose (server event names vary by version).
/// </summary>
public sealed class IncomingCallService : IIncomingCallService, IDisposable
{
    private static readonly TimeSpan CallSuppressionDuration = TimeSpan.FromHours(4);
    private static readonly TimeSpan RoomSuppressionDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DeclinedRoomSuppressionDuration = TimeSpan.FromMinutes(30);

    private readonly IChatWebSocketService _ws;
    private readonly IAuthService _auth;
    private readonly ISystemNotificationService _system;
    private readonly IToastService _toast;
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _suppressedInvites = new(StringComparer.OrdinalIgnoreCase);
    private bool _hooked;
    private IncomingCallInfo? _current;
    private CancellationTokenSource? _ringCts;
    private WaveOutEvent? _ringOut;

    public IncomingCallService(
        IChatWebSocketService ws,
        IAuthService auth,
        ISystemNotificationService system,
        IToastService toast)
    {
        _ws = ws;
        _auth = auth;
        _system = system;
        _toast = toast;
    }

    public bool HasIncoming => Current is not null;

    public IncomingCallInfo? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler? Changed;

    public void Start()
    {
        if (!_auth.IsAuthenticated)
        {
            return;
        }

        lock (_gate)
        {
            if (_hooked)
            {
                return;
            }

            _hooked = true;
            _ws.PacketReceived += OnPacket;
            _auth.AuthenticationStateChanged += OnAuthChanged;
        }

        _ = _ws.ConnectAsync();
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_hooked)
            {
                return;
            }

            _hooked = false;
            _ws.PacketReceived -= OnPacket;
            _auth.AuthenticationStateChanged -= OnAuthChanged;
        }

        StopRing();
        Clear(raise: true);
    }

    public void PresentInvite(IncomingCallInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.RoomId == Guid.Empty)
        {
            return;
        }

        lock (_gate)
        {
            PruneSuppressedInvites_NoLock();
            var inviteKey = GetInviteKey(info);
            if (IsSuppressed_NoLock(inviteKey)
                || IsSuppressed_NoLock(GetRoomKey(info.RoomId)))
            {
                return;
            }

            // Repeated state packets for one invite must not restart ringing/toasts.
            if (_current is { } current
                && string.Equals(GetInviteKey(current), inviteKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _current = info;
        }

        StartRing();
        try
        {
            _system.Show(
                "来电 · " + info.DisplayTitle,
                info.DisplaySubtitle,
                $"solian://chat/{info.RoomId:D}");
        }
        catch
        {
            // ignore
        }

        _toast.ShowInAppOnly($"来电：{info.DisplayTitle} — {info.DisplaySubtitle}");
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkOutgoingCall(Guid roomId, Guid? callId = null)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        var clearedCurrent = false;
        lock (_gate)
        {
            PruneSuppressedInvites_NoLock();
            _suppressedInvites[GetRoomKey(roomId)] = DateTimeOffset.UtcNow + RoomSuppressionDuration;
            if (callId is { } id && id != Guid.Empty)
            {
                _suppressedInvites[$"call:{id:N}"] = DateTimeOffset.UtcNow + CallSuppressionDuration;
            }

            if (_current is { } current
                && (current.RoomId == roomId
                    || (callId is { } currentCallId && current.CallId == currentCallId)))
            {
                _current = null;
                clearedCurrent = true;
            }
        }

        if (clearedCurrent)
        {
            StopRing();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void CancelOutgoingCall(Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        lock (_gate)
        {
            _suppressedInvites.Remove(GetRoomKey(roomId));
        }
    }

    public void Decline()
    {
        lock (_gate)
        {
            SuppressCurrentInvite_NoLock();
            _current = null;
        }

        StopRing();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IncomingCallInfo? Accept()
    {
        StopRing();
        IncomingCallInfo? info;
        lock (_gate)
        {
            info = _current;
            SuppressCurrentInvite_NoLock();
            _current = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return info;
    }

    private void OnAuthChanged(object? sender, EventArgs e)
    {
        if (!_auth.IsAuthenticated)
        {
            StopRing();
            Clear(raise: true);
            return;
        }

        _ = _ws.ConnectAsync();
    }

    private void OnPacket(object? sender, ChatWsPacket packet)
    {
        if (LooksLikeCallEnded(packet.Type))
        {
            HandleCallEnded(packet);
            return;
        }

        if (!LooksLikeCallInvite(packet.Type))
        {
            // Some deployments embed invite in notification / message meta
            if (!LooksLikeCallInvite(packet.RawJson))
            {
                return;
            }
        }

        if (!TryParseInvite(packet, out var info) || info is null)
        {
            return;
        }

        // Ignore own invites if caller id matches me
        var me = _auth.CurrentAccount?.Id;
        if (me is { } myId
            && Guid.TryParse(info.CallerId, out var caller)
            && caller == myId)
        {
            return;
        }

        PresentInvite(info);
    }

    private static bool LooksLikeCallInvite(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var t = text.ToLowerInvariant();
        return t.Contains("call.invite", StringComparison.Ordinal)
               || t.Contains("call_invite", StringComparison.Ordinal)
               || t.Contains("realtime.invite", StringComparison.Ordinal)
               || t.Contains("realtime_invite", StringComparison.Ordinal)
               || string.Equals(t.Trim(), "chat.realtime", StringComparison.Ordinal)
               || (t.Contains("invite", StringComparison.Ordinal) && t.Contains("call", StringComparison.Ordinal))
               || (t.Contains("invite", StringComparison.Ordinal) && t.Contains("realtime", StringComparison.Ordinal))
               || (t.Contains("ring", StringComparison.Ordinal) && t.Contains("call", StringComparison.Ordinal));
    }

    private static bool LooksLikeCallEnded(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var t = text.ToLowerInvariant();
        var isCall = t.Contains("call", StringComparison.Ordinal)
                     || t.Contains("realtime", StringComparison.Ordinal);
        return isCall
               && (t.Contains("ended", StringComparison.Ordinal)
                   || t.Contains("terminated", StringComparison.Ordinal)
                   || t.Contains("hangup", StringComparison.Ordinal));
    }

    private static bool TryParseInvite(ChatWsPacket packet, out IncomingCallInfo? info)
    {
        info = null;
        try
        {
            JsonElement root;
            if (packet.Data is { ValueKind: JsonValueKind.Object } data)
            {
                root = data;
            }
            else if (!string.IsNullOrWhiteSpace(packet.RawJson))
            {
                using var doc = JsonDocument.Parse(packet.RawJson);
                root = doc.RootElement;
                if (root.TryGetProperty("data", out var nested) && nested.ValueKind == JsonValueKind.Object)
                {
                    root = nested;
                }
            }
            else
            {
                return false;
            }

            var roomId = TryGetGuid(root, "room_id", "chat_room_id", "roomId", "chatRoomId");
            if (roomId is null || roomId == Guid.Empty)
            {
                // nested room object
                if (root.TryGetProperty("room", out var room) && room.ValueKind == JsonValueKind.Object)
                {
                    roomId = TryGetGuid(room, "id", "room_id");
                }
            }

            if (roomId is null || roomId == Guid.Empty)
            {
                return false;
            }

            var caller = TryGetString(root, "caller_name", "from_name", "sender_name", "inviter_name", "name")
                         ?? TryNestedName(root, "caller", "from", "sender", "inviter", "account");
            var callerId = TryGetString(root, "caller_id", "from_id", "sender_id", "inviter_id", "account_id")
                           ?? TryNestedId(root, "caller", "from", "sender", "inviter", "account");
            var title = TryGetString(root, "room_title", "room_name", "title")
                        ?? TryNestedName(root, "room", "chat_room");
            var callId = TryGetGuid(root, "call_id", "callId", "session_id", "sessionId");

            info = new IncomingCallInfo
            {
                RoomId = roomId.Value,
                CallId = callId,
                RoomTitle = title,
                CallerName = caller,
                CallerId = callerId,
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Guid? TryGetGuid(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (!el.TryGetProperty(n, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.String && Guid.TryParse(p.GetString(), out var g))
            {
                return g;
            }

            if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out g))
            {
                return g;
            }
        }

        return null;
    }

    private static string? TryGetString(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (el.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var s = p.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }
        }

        return null;
    }

    private static string? TryNestedName(JsonElement el, params string[] objects)
    {
        foreach (var o in objects)
        {
            if (!el.TryGetProperty(o, out var obj) || obj.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var s = TryGetString(obj, "nick", "name", "username", "display_name");
            if (!string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
        }

        return null;
    }

    private static string? TryNestedId(JsonElement el, params string[] objects)
    {
        foreach (var o in objects)
        {
            if (!el.TryGetProperty(o, out var obj) || obj.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var s = TryGetString(obj, "id", "account_id");
            if (!string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
        }

        return null;
    }

    private void Clear(bool raise)
    {
        lock (_gate)
        {
            _current = null;
            _suppressedInvites.Clear();
        }

        if (raise)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandleCallEnded(ChatWsPacket packet)
    {
        if (!TryParseInvite(packet, out var ended) || ended is null)
        {
            return;
        }

        var changed = false;
        lock (_gate)
        {
            var callKey = ended.CallId is { } callId && callId != Guid.Empty
                ? $"call:{callId:N}"
                : null;
            if (callKey is not null)
            {
                _suppressedInvites.Remove(callKey);
            }

            _suppressedInvites.Remove($"room:{ended.RoomId:N}");
            if (_current is { } current
                && (current.RoomId == ended.RoomId
                    || (ended.CallId is { } endedCallId && current.CallId == endedCallId)))
            {
                _current = null;
                changed = true;
            }
        }

        if (changed)
        {
            StopRing();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SuppressCurrentInvite_NoLock()
    {
        if (_current is not { } current)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _suppressedInvites[GetRoomKey(current.RoomId)] = now + DeclinedRoomSuppressionDuration;
        if (current.CallId is { } callId && callId != Guid.Empty)
        {
            _suppressedInvites[GetInviteKey(current)] = now + CallSuppressionDuration;
        }
        PruneSuppressedInvites_NoLock();
    }

    private bool IsSuppressed_NoLock(string key)
        => _suppressedInvites.TryGetValue(key, out var until)
           && until > DateTimeOffset.UtcNow;

    private void PruneSuppressedInvites_NoLock()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _suppressedInvites
                     .Where(pair => pair.Value <= now)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _suppressedInvites.Remove(key);
        }
    }

    private static string GetInviteKey(IncomingCallInfo info)
        => info.CallId is { } callId && callId != Guid.Empty
            ? $"call:{callId:N}"
            : GetRoomKey(info.RoomId);

    private static string GetRoomKey(Guid roomId) => $"room:{roomId:N}";

    private void StartRing()
    {
        StopRing();
        _ringCts = new CancellationTokenSource();
        var ct = _ringCts.Token;
        _ = RingLoopAsync(ct);
    }

    private async Task RingLoopAsync(CancellationToken ct)
    {
        try
        {
            // Soft two-tone ring using NAudio signal generator
            var toneA = new SignalGenerator(44100, 1)
            {
                Gain = 0.12,
                Frequency = 480,
                Type = SignalGeneratorType.Sin,
            };
            var toneB = new SignalGenerator(44100, 1)
            {
                Gain = 0.12,
                Frequency = 620,
                Type = SignalGeneratorType.Sin,
            };

            while (!ct.IsCancellationRequested)
            {
                await PlayToneAsync(toneA, 400, ct).ConfigureAwait(false);
                await PlayToneAsync(toneB, 400, ct).ConfigureAwait(false);
                await Task.Delay(900, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        catch
        {
            // ignore audio device errors
        }
    }

    private async Task PlayToneAsync(ISampleProvider tone, int ms, CancellationToken ct)
    {
        var limited = tone.Take(TimeSpan.FromMilliseconds(ms));
        using var wo = new WaveOutEvent { DesiredLatency = 80 };
        wo.Init(limited.ToWaveProvider16());
        wo.Play();
        var start = Environment.TickCount64;
        while (wo.PlaybackState == PlaybackState.Playing && !ct.IsCancellationRequested)
        {
            if (Environment.TickCount64 - start > ms + 200)
            {
                break;
            }

            await Task.Delay(20, ct).ConfigureAwait(false);
        }

        try
        {
            wo.Stop();
        }
        catch
        {
            // ignore
        }
    }

    private void StopRing()
    {
        try
        {
            _ringCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            _ringCts?.Dispose();
        }
        catch
        {
            // ignore
        }

        _ringCts = null;
        try
        {
            _ringOut?.Stop();
            _ringOut?.Dispose();
        }
        catch
        {
            // ignore
        }

        _ringOut = null;
    }

    public void Dispose()
    {
        Stop();
    }
}
