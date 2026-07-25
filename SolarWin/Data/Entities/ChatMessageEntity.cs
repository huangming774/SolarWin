namespace SolarWin.Data.Entities;

/// <summary>
/// Local SQLite row for a chat message (design v1.0 Phase 1).
/// Not the API DTO — map from/to <c>SnChatMessage</c> in later phases.
/// </summary>
public sealed class ChatMessageEntity
{
    /// <summary>Local row identity (always set; never Guid.Empty).</summary>
    public Guid RowId { get; set; }

    /// <summary>Server message id; may be Empty for optimistic local-only rows.</summary>
    public Guid MessageId { get; set; }

    public Guid RoomId { get; set; }

    /// <summary>Room-monotonic sequence from the server; 0 until confirmed.</summary>
    public long RoomSequence { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? Type { get; set; }

    public string? Content { get; set; }

    public Guid SenderId { get; set; }

    public string? ClientMessageId { get; set; }

    public bool IsEncrypted { get; set; }

    public Guid? RepliedMessageId { get; set; }

    public Guid? ForwardedMessageId { get; set; }

    public byte[]? Ciphertext { get; set; }

    public byte[]? EncryptionHeader { get; set; }

    public byte[]? EncryptionSignature { get; set; }

    public string? EncryptionScheme { get; set; }

    public long? EncryptionEpoch { get; set; }

    public string? EncryptionMessageType { get; set; }

    public string? Nonce { get; set; }

    /// <summary>Raw API DTO JSON snapshot (pre-decryption wire shape).</summary>
    public string? PayloadJson { get; set; }

    public DateTimeOffset SyncedAt { get; set; }

    public ChatMessageSource Source { get; set; }
}
