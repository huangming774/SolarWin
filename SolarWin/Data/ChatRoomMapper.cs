using System.Text.Json;
using SolarWin.Data.Entities;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Data;

/// <summary>
/// Maps room list rows ↔ API DTOs (Phase 6).
/// Preview ciphertext lives in BLOB columns only (same rules as chat_messages).
/// </summary>
public static class ChatRoomMapper
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;
    private const int PreviewMaxChars = 200;

    public static ChatRoomEntity ToEntity(
        SnChatRoom room,
        ChatSummaryResponse? summary,
        ChatMessageSource source,
        long? lastReadSequence = null)
    {
        ArgumentNullException.ThrowIfNull(room);

        var entity = new ChatRoomEntity
        {
            RoomId = room.Id,
            Source = source,
            SyncedAt = DateTimeOffset.UtcNow,
        };
        ApplyRoomAndSummary(entity, room, summary, lastReadSequence, replacePreviewBlobs: true);
        return entity;
    }

    public static void UpdateEntity(
        ChatRoomEntity entity,
        SnChatRoom room,
        ChatSummaryResponse? summary,
        ChatMessageSource? source = null,
        long? lastReadSequence = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(room);

        if (source is { } s)
        {
            entity.Source = s;
        }

        entity.SyncedAt = DateTimeOffset.UtcNow;
        ApplyRoomAndSummary(entity, room, summary, lastReadSequence, replacePreviewBlobs: true);
    }

    public static void ApplySummaryPreview(ChatRoomEntity entity, ChatSummaryResponse summary)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(summary);

        entity.UnreadCount = Math.Max(0, summary.UnreadCount);
        ApplyLastMessage(entity, summary.LastMessage);
        entity.SyncedAt = DateTimeOffset.UtcNow;
    }

    public static void ApplyLastMessage(ChatRoomEntity entity, SnChatMessage? last)
    {
        if (last is null)
        {
            entity.LastMessageId = null;
            entity.LastMessageSequence = null;
            entity.LastMessageType = null;
            entity.LastMessageAt = null;
            ClearPreview(entity);
            return;
        }

        entity.LastMessageId = last.Id == Guid.Empty ? null : last.Id;
        entity.LastMessageSequence = last.RoomSequence > 0 ? last.RoomSequence : null;
        entity.LastMessageType = last.Type;
        entity.LastMessageAt = last.CreatedAt ?? last.UpdatedAt;
        if (entity.LastMessageAt is { } at)
        {
            entity.LastActivity = at;
        }

        if (last.IsEncrypted)
        {
            entity.PreviewIsEncrypted = true;
            entity.PreviewPlaintext = null;
            entity.PreviewCiphertext = CloneBytes(last.Ciphertext);
            entity.PreviewEncryptionHeader = CloneBytes(last.EncryptionHeader);
            entity.PreviewEncryptionSignature = CloneBytes(last.EncryptionSignature);
            entity.PreviewEncryptionScheme = last.EncryptionScheme;
            entity.PreviewEncryptionEpoch = last.EncryptionEpoch;
            entity.PreviewNonce = last.Nonce;
        }
        else
        {
            entity.PreviewIsEncrypted = false;
            entity.PreviewCiphertext = null;
            entity.PreviewEncryptionHeader = null;
            entity.PreviewEncryptionSignature = null;
            entity.PreviewEncryptionScheme = null;
            entity.PreviewEncryptionEpoch = null;
            entity.PreviewNonce = null;
            entity.PreviewPlaintext = TruncatePreview(last.Content)
                ?? (string.IsNullOrWhiteSpace(last.Type) || last.Type == "text"
                    ? "（消息）"
                    : $"[{last.Type}]");
        }
    }

    /// <summary>UI list projection (plaintext preview only; decrypt pipeline can replace later).</summary>
    public static string ResolvePreviewForUi(ChatRoomEntity entity)
    {
        if (entity.PreviewIsEncrypted)
        {
            return "[加密消息]";
        }

        if (!string.IsNullOrWhiteSpace(entity.PreviewPlaintext))
        {
            return entity.PreviewPlaintext!;
        }

        if (!string.IsNullOrWhiteSpace(entity.LastMessageType)
            && !string.Equals(entity.LastMessageType, "text", StringComparison.OrdinalIgnoreCase))
        {
            return $"[{entity.LastMessageType}]";
        }

        return "暂无消息";
    }

    public static SnChatRoom ToRoomDto(ChatRoomEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        SnChatRoom room;
        if (!string.IsNullOrWhiteSpace(entity.PayloadJson))
        {
            try
            {
                room = JsonSerializer.Deserialize<SnChatRoom>(entity.PayloadJson, JsonOptions)
                       ?? new SnChatRoom { Id = entity.RoomId };
            }
            catch
            {
                room = new SnChatRoom { Id = entity.RoomId };
            }
        }
        else
        {
            room = new SnChatRoom { Id = entity.RoomId };
        }

        room.Id = entity.RoomId;
        room.Type = (ChatRoomType)entity.Type;
        room.Name = entity.Name ?? room.Name;
        room.IsCommunity = entity.IsCommunity;
        room.IsPublic = entity.IsPublic;
        room.EncryptionMode = (ChatRoomEncryptionMode)entity.EncryptionMode;
        room.UpdatedAt = entity.LastActivity;
        if (!string.IsNullOrWhiteSpace(entity.AvatarFileId) && room.Picture is null)
        {
            room.Picture = new SnCloudFile { Id = entity.AvatarFileId };
        }

        return room;
    }

    public static ChatSummaryResponse ToSummary(ChatRoomEntity entity)
    {
        var summary = new ChatSummaryResponse
        {
            UnreadCount = entity.UnreadCount,
        };

        if (entity.LastMessageId is not null
            || entity.LastMessageSequence is not null
            || entity.PreviewIsEncrypted
            || !string.IsNullOrWhiteSpace(entity.PreviewPlaintext))
        {
            summary.LastMessage = new SnChatMessage
            {
                Id = entity.LastMessageId ?? Guid.Empty,
                ChatRoomId = entity.RoomId,
                RoomSequence = entity.LastMessageSequence ?? 0,
                Type = entity.LastMessageType,
                CreatedAt = entity.LastMessageAt,
                IsEncrypted = entity.PreviewIsEncrypted,
                Content = entity.PreviewIsEncrypted ? null : entity.PreviewPlaintext,
                Ciphertext = CloneBytes(entity.PreviewCiphertext),
                EncryptionHeader = CloneBytes(entity.PreviewEncryptionHeader),
                EncryptionSignature = CloneBytes(entity.PreviewEncryptionSignature),
                EncryptionScheme = entity.PreviewEncryptionScheme,
                EncryptionEpoch = entity.PreviewEncryptionEpoch,
                Nonce = entity.PreviewNonce,
            };
        }

        return summary;
    }

    private static void ApplyRoomAndSummary(
        ChatRoomEntity entity,
        SnChatRoom room,
        ChatSummaryResponse? summary,
        long? lastReadSequence,
        bool replacePreviewBlobs)
    {
        entity.RoomId = room.Id;
        entity.Type = (int)room.Type;
        entity.Name = room.Name;
        entity.IsCommunity = room.IsCommunity;
        entity.IsPublic = room.IsPublic;
        entity.EncryptionMode = (int)room.EncryptionMode;
        entity.AvatarFileId = CloudFileUrlHelper.ResolveFileId(room.Picture);
        entity.DeletedAt = room.DeletedAt;
        entity.PayloadJson = SerializeRoomPayload(room);

        var activity = summary?.LastMessage?.CreatedAt
                       ?? room.UpdatedAt
                       ?? room.CreatedAt
                       ?? DateTimeOffset.UtcNow;
        entity.LastActivity = activity;

        if (summary is not null)
        {
            entity.UnreadCount = Math.Max(0, summary.UnreadCount);
            ApplyLastMessage(entity, summary.LastMessage);
        }
        else if (entity.LastActivity == default)
        {
            entity.LastActivity = DateTimeOffset.UtcNow;
        }

        if (lastReadSequence is { } seq && seq > entity.LastReadSequence)
        {
            entity.LastReadSequence = seq;
        }
    }

    private static void ClearPreview(ChatRoomEntity entity)
    {
        entity.PreviewIsEncrypted = false;
        entity.PreviewPlaintext = null;
        entity.PreviewCiphertext = null;
        entity.PreviewEncryptionHeader = null;
        entity.PreviewEncryptionSignature = null;
        entity.PreviewEncryptionScheme = null;
        entity.PreviewEncryptionEpoch = null;
        entity.PreviewNonce = null;
    }

    private static string SerializeRoomPayload(SnChatRoom room)
    {
        // Snapshot without rewriting large member graphs if null-heavy.
        return JsonSerializer.Serialize(room, JsonOptions);
    }

    private static string? TruncatePreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var t = content.Trim().Replace('\n', ' ');
        return t.Length <= PreviewMaxChars ? t : t[..PreviewMaxChars] + "…";
    }

    private static byte[]? CloneBytes(byte[]? source)
        => source is { Length: > 0 } ? (byte[])source.Clone() : source is { Length: 0 } ? [] : null;
}
