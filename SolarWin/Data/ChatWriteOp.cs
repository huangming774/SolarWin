using SolarWin.Data.Entities;
using SolarWin.Models;

namespace SolarWin.Data;

/// <summary>Write-side operations for the single-consumer SQLite pump (design v1.0 Phase 2).</summary>
public abstract record ChatWriteOp;

/// <summary>Upsert one message (WS / send / API echo).</summary>
public sealed record UpsertMessageOp(
    Guid RoomId,
    SnChatMessage Message,
    ChatMessageSource Source) : ChatWriteOp;

/// <summary>Upsert a batch (SetRoomMessages page).</summary>
public sealed record UpsertMessagesBatchOp(
    Guid RoomId,
    IReadOnlyList<SnChatMessage> Messages,
    ChatMessageSource Source) : ChatWriteOp;

/// <summary>Delete a single message row by server id (and optional client id).</summary>
public sealed record DeleteMessageOp(
    Guid RoomId,
    Guid MessageId,
    string? ClientMessageId = null) : ChatWriteOp;

/// <summary>Delete all local rows for a room (cache invalidate).</summary>
public sealed record DeleteRoomMessagesOp(Guid RoomId) : ChatWriteOp;

/// <summary>Run full-account eviction once (design §1.7).</summary>
public sealed record RunFullEvictionOp : ChatWriteOp;

// —— Phase 6: rooms list ——

/// <summary>Upsert one room row (full DTO + optional summary).</summary>
public sealed record UpsertRoomOp(
    SnChatRoom Room,
    ChatSummaryResponse? Summary,
    ChatMessageSource Source,
    long? LastReadSequence = null) : ChatWriteOp;

/// <summary>Upsert many rooms (full pull). When <see cref="RemoveMissing"/>, delete rows not in the batch.</summary>
public sealed record UpsertRoomsBatchOp(
    IReadOnlyList<(SnChatRoom Room, ChatSummaryResponse? Summary)> Rooms,
    ChatMessageSource Source,
    bool RemoveMissing) : ChatWriteOp;

/// <summary>Single-row read mark.</summary>
public sealed record UpdateRoomReadOp(
    Guid RoomId,
    long LastReadSequence,
    int UnreadCount = 0) : ChatWriteOp;

/// <summary>Socket / message-driven preview + unread bump.</summary>
public sealed record UpdateRoomPreviewOp(
    Guid RoomId,
    SnChatMessage? LastMessage,
    int? UnreadCount,
    DateTimeOffset? LastActivity = null) : ChatWriteOp;

/// <summary>Hard-delete one room list row (not message history).</summary>
public sealed record DeleteRoomListOp(Guid RoomId) : ChatWriteOp;
