namespace SolarWin.Data.Entities;

/// <summary>Local SQLite row for chat room list (Phase 6 design).</summary>
public sealed class ChatRoomEntity
{
    public Guid RoomId { get; set; }

    public int Type { get; set; }

    public bool Pinned { get; set; }

    public DateTimeOffset LastActivity { get; set; }

    public int UnreadCount { get; set; }

    public long LastReadSequence { get; set; }

    public string? Name { get; set; }

    public bool IsCommunity { get; set; }

    public bool IsPublic { get; set; }

    public int EncryptionMode { get; set; }

    public string? AvatarFileId { get; set; }

    public Guid? LastMessageId { get; set; }

    public long? LastMessageSequence { get; set; }

    public string? LastMessageType { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }

    public bool PreviewIsEncrypted { get; set; }

    public byte[]? PreviewCiphertext { get; set; }

    public byte[]? PreviewEncryptionHeader { get; set; }

    public byte[]? PreviewEncryptionSignature { get; set; }

    public string? PreviewEncryptionScheme { get; set; }

    public long? PreviewEncryptionEpoch { get; set; }

    public string? PreviewNonce { get; set; }

    /// <summary>Plain preview when not encrypted; null when encrypted.</summary>
    public string? PreviewPlaintext { get; set; }

    public string? PayloadJson { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset SyncedAt { get; set; }

    public ChatMessageSource Source { get; set; }
}
