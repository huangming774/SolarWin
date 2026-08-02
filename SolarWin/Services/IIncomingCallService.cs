namespace SolarWin.Services;

/// <summary>
/// Detects remote realtime-call invites (WebSocket / ring) and exposes accept/decline UI state.
/// </summary>
public interface IIncomingCallService
{
    bool HasIncoming { get; }

    IncomingCallInfo? Current { get; }

    event EventHandler? Changed;

    void Start();

    void Stop();

    /// <summary>Manually raise an invite (e.g. local test or parsed notification).</summary>
    void PresentInvite(IncomingCallInfo info);

    /// <summary>Prevent the locally initiated call from being presented as an incoming invite.</summary>
    void MarkOutgoingCall(Guid roomId, Guid? callId = null);

    /// <summary>Remove the short-lived room suppression when creating a call failed.</summary>
    void CancelOutgoingCall(Guid roomId);

    void Decline();

    /// <summary>
    /// Accept: clears banner and returns the invite for the UI to join.
    /// </summary>
    IncomingCallInfo? Accept();
}

public sealed class IncomingCallInfo
{
    public required Guid RoomId { get; init; }

    /// <summary>Server-side call session id when supplied by the invite event.</summary>
    public Guid? CallId { get; init; }

    public string? RoomTitle { get; init; }

    public string? CallerName { get; init; }

    public string? CallerId { get; init; }

    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.Now;

    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(CallerName) ? "来电" : CallerName!;

    public string DisplaySubtitle =>
        string.IsNullOrWhiteSpace(RoomTitle)
            ? $"房间 {RoomId.ToString("D")[..8]}…"
            : RoomTitle!;
}
