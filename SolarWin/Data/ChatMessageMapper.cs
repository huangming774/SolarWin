using System.Text.Json;
using SolarWin.Data.Entities;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Data;

/// <summary>
/// Maps <see cref="SnChatMessage"/> ↔ <see cref="ChatMessageEntity"/>.
/// PayloadJson = raw API DTO without encryption byte[] (those live in BLOB columns).
/// </summary>
public static class ChatMessageMapper
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    public static ChatMessageEntity ToEntity(
        SnChatMessage dto,
        Guid roomId,
        ChatMessageSource source,
        DateTimeOffset? syncedAt = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var effectiveRoomId = roomId != Guid.Empty ? roomId : dto.ChatRoomId;
        var entity = new ChatMessageEntity
        {
            RowId = Guid.NewGuid(),
            SyncedAt = syncedAt ?? DateTimeOffset.UtcNow,
            Source = source,
        };
        ApplyDtoToEntity(entity, dto, effectiveRoomId, replaceBlobs: true);
        return entity;
    }

    /// <summary>Overwrite scalar/BLOB/payload on an existing row (keeps <see cref="ChatMessageEntity.RowId"/>).</summary>
    public static void UpdateEntity(
        ChatMessageEntity entity,
        SnChatMessage dto,
        Guid roomId,
        ChatMessageSource? source = null,
        DateTimeOffset? syncedAt = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(dto);

        var effectiveRoomId = roomId != Guid.Empty ? roomId : dto.ChatRoomId;
        if (source is { } s)
        {
            entity.Source = s;
        }

        if (syncedAt is { } t)
        {
            entity.SyncedAt = t;
        }
        else
        {
            entity.SyncedAt = DateTimeOffset.UtcNow;
        }

        ApplyDtoToEntity(entity, dto, effectiveRoomId, replaceBlobs: true);
    }

    public static SnChatMessage ToDto(ChatMessageEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        SnChatMessage dto;
        if (!string.IsNullOrWhiteSpace(entity.PayloadJson))
        {
            try
            {
                dto = JsonSerializer.Deserialize<SnChatMessage>(entity.PayloadJson, JsonOptions)
                      ?? new SnChatMessage();
            }
            catch
            {
                dto = new SnChatMessage();
            }
        }
        else
        {
            dto = new SnChatMessage();
        }

        // Column scalars + BLOBs are authoritative for indexed / crypto fields.
        dto.Id = entity.MessageId;
        dto.ChatRoomId = entity.RoomId;
        dto.RoomSequence = entity.RoomSequence;
        dto.CreatedAt = entity.CreatedAt;
        dto.UpdatedAt = entity.UpdatedAt;
        dto.DeletedAt = entity.DeletedAt;
        dto.Type = entity.Type;
        dto.Content = entity.Content;
        dto.SenderId = entity.SenderId;
        dto.ClientMessageId = entity.ClientMessageId;
        dto.IsEncrypted = entity.IsEncrypted;
        dto.RepliedMessageId = entity.RepliedMessageId;
        dto.ForwardedMessageId = entity.ForwardedMessageId;
        dto.EncryptionScheme = entity.EncryptionScheme;
        dto.EncryptionEpoch = entity.EncryptionEpoch;
        dto.EncryptionMessageType = entity.EncryptionMessageType;
        dto.Nonce = entity.Nonce;
        dto.Ciphertext = CloneBytes(entity.Ciphertext);
        dto.EncryptionHeader = CloneBytes(entity.EncryptionHeader);
        dto.EncryptionSignature = CloneBytes(entity.EncryptionSignature);

        return dto;
    }

    private static void ApplyDtoToEntity(
        ChatMessageEntity entity,
        SnChatMessage dto,
        Guid roomId,
        bool replaceBlobs)
    {
        entity.MessageId = dto.Id;
        entity.RoomId = roomId;
        entity.RoomSequence = dto.RoomSequence;
        entity.CreatedAt = dto.CreatedAt;
        entity.UpdatedAt = dto.UpdatedAt;
        entity.DeletedAt = dto.DeletedAt;
        entity.Type = dto.Type;
        entity.Content = dto.Content;
        entity.SenderId = dto.SenderId;
        entity.ClientMessageId = string.IsNullOrWhiteSpace(dto.ClientMessageId)
            ? null
            : dto.ClientMessageId;
        entity.IsEncrypted = dto.IsEncrypted;
        entity.RepliedMessageId = dto.RepliedMessageId;
        entity.ForwardedMessageId = dto.ForwardedMessageId;
        entity.EncryptionScheme = dto.EncryptionScheme;
        entity.EncryptionEpoch = dto.EncryptionEpoch;
        entity.EncryptionMessageType = dto.EncryptionMessageType;
        entity.Nonce = dto.Nonce;

        if (replaceBlobs)
        {
            entity.Ciphertext = CloneBytes(dto.Ciphertext);
            entity.EncryptionHeader = CloneBytes(dto.EncryptionHeader);
            entity.EncryptionSignature = CloneBytes(dto.EncryptionSignature);
        }

        entity.PayloadJson = SerializePayloadWithoutBlobs(dto);
    }

    /// <summary>
    /// Serialize DTO for PayloadJson with encryption byte[] omitted.
    /// Does <b>not</b> mutate <paramref name="dto"/>. Fast path skips blob encode when none present;
    /// otherwise serializes a shallow projection without blob fields (no full-graph blob base64).
    /// </summary>
    public static string SerializePayloadWithoutBlobs(SnChatMessage dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Ciphertext is null && dto.EncryptionHeader is null && dto.EncryptionSignature is null)
        {
            return JsonSerializer.Serialize(dto, JsonOptions);
        }

        // Projection: same public shape, blobs left null so WhenWritingNull omits them.
        // New instance — never mutates caller's dto (shared instances / throw safety).
        var projection = new SnChatMessage
        {
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            DeletedAt = dto.DeletedAt,
            Id = dto.Id,
            RoomSequence = dto.RoomSequence,
            Type = dto.Type,
            Content = dto.Content,
            IsEncrypted = dto.IsEncrypted,
            Ciphertext = null,
            EncryptionHeader = null,
            EncryptionSignature = null,
            EncryptionScheme = dto.EncryptionScheme,
            EncryptionEpoch = dto.EncryptionEpoch,
            EncryptionMessageType = dto.EncryptionMessageType,
            ClientMessageId = dto.ClientMessageId,
            Meta = dto.Meta,
            MembersMentioned = dto.MembersMentioned,
            Nonce = dto.Nonce,
            EditedAt = dto.EditedAt,
            Attachments = dto.Attachments,
            ReactionsCount = dto.ReactionsCount,
            ReactionsMade = dto.ReactionsMade,
            Reactions = dto.Reactions,
            RepliedMessageId = dto.RepliedMessageId,
            RepliedMessage = dto.RepliedMessage,
            ForwardedMessageId = dto.ForwardedMessageId,
            ForwardedMessage = dto.ForwardedMessage,
            SenderId = dto.SenderId,
            Sender = dto.Sender,
            ChatRoomId = dto.ChatRoomId,
            ChatRoom = dto.ChatRoom,
            ResourceIdentifier = dto.ResourceIdentifier,
        };

        return JsonSerializer.Serialize(projection, JsonOptions);
    }

    private static byte[]? CloneBytes(byte[]? source)
        => source is { Length: > 0 } ? (byte[])source.Clone() : source is { Length: 0 } ? [] : null;
}
