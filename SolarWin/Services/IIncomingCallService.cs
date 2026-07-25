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

    void Decline();

    /// <summary>
    /// Accept: clears banner and returns the invite for the UI to join.
    /// </summary>
    IncomingCallInfo? Accept();
}

public sealed class IncomingCallInfo
{
    public required Guid RoomId { get; init; }

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
