using System.Text.Json;
using SolarWin.Data;
using SolarWin.Data.Entities;
using SolarWin.Models;

namespace SolarWin.Tests;

public class ChatMessageMapperTests
{
    [Fact]
    public void RoundTrip_Preserves_ByteArrays_And_Meta()
    {
        var metaJson = """{"sticker_id":"abc-123","flag":true,"n":42}""";
        using var metaDoc = JsonDocument.Parse(metaJson);
        var meta = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["sticker_id"] = metaDoc.RootElement.GetProperty("sticker_id").Clone(),
            ["flag"] = metaDoc.RootElement.GetProperty("flag").Clone(),
            ["n"] = metaDoc.RootElement.GetProperty("n").Clone(),
        };

        var cipher = new byte[] { 0x01, 0x02, 0xFE, 0xFF };
        var header = new byte[] { 0x10, 0x20, 0x30 };
        var signature = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE };

        var roomId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var original = new SnChatMessage
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ChatRoomId = roomId,
            RoomSequence = 99,
            Type = "text",
            Content = "hello",
            SenderId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ClientMessageId = "client-xyz",
            IsEncrypted = true,
            Ciphertext = cipher,
            EncryptionHeader = header,
            EncryptionSignature = signature,
            EncryptionScheme = "mls",
            EncryptionEpoch = 7,
            EncryptionMessageType = "application",
            Nonce = "nonce-1",
            Meta = meta,
            MembersMentioned = [Guid.Parse("44444444-4444-4444-4444-444444444444")],
            CreatedAt = DateTimeOffset.Parse("2026-01-15T12:00:00Z"),
        };

        var entity = ChatMessageMapper.ToEntity(original, roomId, ChatMessageSource.Api);

        // Blobs on columns; not embedded as the sole storage in JSON.
        Assert.Equal(cipher, entity.Ciphertext);
        Assert.Equal(header, entity.EncryptionHeader);
        Assert.Equal(signature, entity.EncryptionSignature);
        Assert.False(string.IsNullOrWhiteSpace(entity.PayloadJson));
        Assert.DoesNotContain("AQL+/w", entity.PayloadJson!, StringComparison.Ordinal); // base64 of cipher unlikely required
        // Payload must not re-store ciphertext as JSON field with content (stripped before serialize).
        Assert.DoesNotContain("\"ciphertext\"", entity.PayloadJson!, StringComparison.OrdinalIgnoreCase);

        var roundTrip = ChatMessageMapper.ToDto(entity);

        Assert.Equal(original.Id, roundTrip.Id);
        Assert.Equal(roomId, roundTrip.ChatRoomId);
        Assert.Equal(original.RoomSequence, roundTrip.RoomSequence);
        Assert.Equal(original.Content, roundTrip.Content);
        Assert.Equal(original.ClientMessageId, roundTrip.ClientMessageId);
        Assert.True(roundTrip.IsEncrypted);
        Assert.Equal(cipher, roundTrip.Ciphertext);
        Assert.Equal(header, roundTrip.EncryptionHeader);
        Assert.Equal(signature, roundTrip.EncryptionSignature);
        Assert.Equal(original.EncryptionScheme, roundTrip.EncryptionScheme);
        Assert.Equal(original.EncryptionEpoch, roundTrip.EncryptionEpoch);
        Assert.Equal(original.EncryptionMessageType, roundTrip.EncryptionMessageType);
        Assert.Equal(original.Nonce, roundTrip.Nonce);

        Assert.NotNull(roundTrip.Meta);
        Assert.True(roundTrip.Meta!.ContainsKey("sticker_id"));
        Assert.Equal(JsonValueKind.String, roundTrip.Meta["sticker_id"].ValueKind);
        Assert.Equal("abc-123", roundTrip.Meta["sticker_id"].GetString());
        Assert.True(roundTrip.Meta["flag"].GetBoolean());
        Assert.Equal(42, roundTrip.Meta["n"].GetInt32());

        Assert.NotNull(roundTrip.MembersMentioned);
        Assert.Single(roundTrip.MembersMentioned!);
        Assert.Equal(original.MembersMentioned![0], roundTrip.MembersMentioned[0]);
    }

    [Fact]
    public void UpdateEntity_Keeps_RowId_Replaces_Payload_And_Blobs()
    {
        var roomId = Guid.NewGuid();
        var first = new SnChatMessage
        {
            Id = Guid.NewGuid(),
            ChatRoomId = roomId,
            RoomSequence = 1,
            Content = "v1",
            Ciphertext = [1, 2, 3],
        };
        var entity = ChatMessageMapper.ToEntity(first, roomId, ChatMessageSource.LocalSend);
        var rowId = entity.RowId;

        var second = new SnChatMessage
        {
            Id = first.Id,
            ChatRoomId = roomId,
            RoomSequence = 1,
            Content = "v2",
            Ciphertext = [9, 8, 7, 6],
            Meta = new Dictionary<string, JsonElement>
            {
                ["k"] = JsonDocument.Parse("\"v\"").RootElement.Clone(),
            },
        };

        ChatMessageMapper.UpdateEntity(entity, second, roomId, ChatMessageSource.Api);

        Assert.Equal(rowId, entity.RowId);
        Assert.Equal("v2", entity.Content);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, entity.Ciphertext);
        Assert.Equal(ChatMessageSource.Api, entity.Source);

        var dto = ChatMessageMapper.ToDto(entity);
        Assert.Equal("v2", dto.Content);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, dto.Ciphertext);
        Assert.NotNull(dto.Meta);
        Assert.Equal("v", dto.Meta!["k"].GetString());
    }

    [Fact]
    public void SerializePayloadWithoutBlobs_Does_Not_Mutate_Source_And_Omits_Blobs()
    {
        var cipher = new byte[] { 1, 2, 3 };
        var header = new byte[] { 4 };
        var signature = new byte[] { 5, 6 };
        var dto = new SnChatMessage
        {
            Id = Guid.NewGuid(),
            Ciphertext = cipher,
            EncryptionHeader = header,
            EncryptionSignature = signature,
            Content = "x",
            IsEncrypted = true,
        };

        var json = ChatMessageMapper.SerializePayloadWithoutBlobs(dto);

        // Source instance never cleared (integrity under throw/race).
        Assert.Same(cipher, dto.Ciphertext);
        Assert.Same(header, dto.EncryptionHeader);
        Assert.Same(signature, dto.EncryptionSignature);
        Assert.Equal(new byte[] { 1, 2, 3 }, dto.Ciphertext);

        // Payload must not embed ciphertext property content.
        Assert.DoesNotContain("\"ciphertext\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"encryption_header\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"encryption_signature\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"content\"", json, StringComparison.OrdinalIgnoreCase);
    }
}
