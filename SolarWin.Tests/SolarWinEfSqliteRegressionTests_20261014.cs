using Microsoft.EntityFrameworkCore;
using SolarWin.Data;
using SolarWin.Data.Entities;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Tests;

public sealed class SolarWinEfSqliteRegressionTests_20261014
{
    [Fact]
    public async Task RoomStore_OrdersDifferentOffsetsByInstant()
    {
        await using var fixture = new SolarWinEfSqliteRegressionFixture();
        var accountId = Guid.NewGuid();
        await fixture.PrepareAsync(accountId);

        var olderInstant = Guid.NewGuid();
        var newerInstant = Guid.NewGuid();
        await using (var db = fixture.CreateDbContext(accountId))
        {
            db.Rooms.AddRange(
                NewRoom(
                    olderInstant,
                    new DateTimeOffset(2026, 10, 13, 1, 0, 0, TimeSpan.FromHours(8))),
                NewRoom(
                    newerInstant,
                    new DateTimeOffset(2026, 10, 13, 0, 30, 0, TimeSpan.Zero)));
            await db.SaveChangesAsync();
        }

        var rows = await new RoomLocalStore(fixture).GetRoomsOrderedAsync();

        Assert.Equal([newerInstant, olderInstant], rows.Select(row => row.RoomId));
    }

    [Fact]
    public async Task ChatWritePump_DeleteThenUpsertAndUpsertThenDeleteKeepBatchOrder()
    {
        var roomId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await using (var fixture = new SolarWinEfSqliteRegressionFixture())
        {
            var accountId = Guid.NewGuid();
            await fixture.PrepareAsync(accountId);
            await SeedMessageAsync(fixture, roomId, messageId, "before");

            await using var pump = new ChatWritePump(fixture);
            pump.EnsureRunning();
            pump.Enqueue(new DeleteMessageOp(roomId, messageId));
            pump.Enqueue(new UpsertMessageOp(roomId, Message(roomId, messageId, "after"), ChatMessageSource.Api));
            await pump.CompleteAndDrainAsync();

            await using var db = fixture.CreateDbContext(accountId);
            var row = Assert.Single(await db.Messages.AsNoTracking().ToListAsync());
            Assert.Equal("after", row.Content);
        }

        await using (var fixture = new SolarWinEfSqliteRegressionFixture())
        {
            var accountId = Guid.NewGuid();
            await fixture.PrepareAsync(accountId);
            await SeedMessageAsync(fixture, roomId, messageId, "before");

            await using var pump = new ChatWritePump(fixture);
            pump.EnsureRunning();
            pump.Enqueue(new UpsertMessageOp(roomId, Message(roomId, messageId, "updated"), ChatMessageSource.Api));
            pump.Enqueue(new DeleteMessageOp(roomId, messageId));
            await pump.CompleteAndDrainAsync();

            await using var db = fixture.CreateDbContext(accountId);
            Assert.Empty(await db.Messages.AsNoTracking().ToListAsync());
        }
    }

    [Fact]
    public async Task ChatWritePump_NewUpsertDeleteAndDeleteUpsertDeletePreserveOrderedSemantics()
    {
        var roomId = Guid.NewGuid();
        var firstMessageId = Guid.NewGuid();
        var secondMessageId = Guid.NewGuid();

        await using (var fixture = new SolarWinEfSqliteRegressionFixture())
        {
            var accountId = Guid.NewGuid();
            await fixture.PrepareAsync(accountId);

            await using var pump = new ChatWritePump(fixture);
            pump.EnsureRunning();
            pump.Enqueue(new UpsertMessageOp(
                roomId,
                Message(roomId, firstMessageId, "new then deleted"),
                ChatMessageSource.Api));
            pump.Enqueue(new DeleteMessageOp(roomId, firstMessageId));
            await pump.CompleteAndDrainAsync();

            await using var db = fixture.CreateDbContext(accountId);
            Assert.Empty(await db.Messages.AsNoTracking().ToListAsync());
        }

        await using (var fixture = new SolarWinEfSqliteRegressionFixture())
        {
            var accountId = Guid.NewGuid();
            await fixture.PrepareAsync(accountId);

            await using var pump = new ChatWritePump(fixture);
            pump.EnsureRunning();
            pump.Enqueue(new DeleteMessageOp(roomId, secondMessageId));
            pump.Enqueue(new UpsertMessageOp(
                roomId,
                Message(roomId, secondMessageId, "recreated"),
                ChatMessageSource.Api));
            pump.Enqueue(new DeleteMessageOp(roomId, secondMessageId));
            await pump.CompleteAndDrainAsync();

            await using var db = fixture.CreateDbContext(accountId);
            Assert.Empty(await db.Messages.AsNoTracking().ToListAsync());
        }
    }

    [Fact]
    public async Task ChatWritePump_UpsertDeleteUpsertKeepsThePostDeleteRow()
    {
        await using var fixture = new SolarWinEfSqliteRegressionFixture();
        var accountId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        await fixture.PrepareAsync(accountId);

        await using var pump = new ChatWritePump(fixture);
        pump.EnsureRunning();
        pump.Enqueue(new UpsertMessageOp(roomId, Message(roomId, messageId, "before delete"), ChatMessageSource.Api));
        pump.Enqueue(new DeleteMessageOp(roomId, messageId));
        pump.Enqueue(new UpsertMessageOp(roomId, Message(roomId, messageId, "after delete"), ChatMessageSource.Api));
        await pump.CompleteAndDrainAsync();

        await using var db = fixture.CreateDbContext(accountId);
        var row = Assert.Single(await db.Messages.AsNoTracking().ToListAsync());
        Assert.Equal("after delete", row.Content);
    }

    [Fact]
    public async Task ChatWritePump_RetentionUsesInstantNotDateTimeOffsetTextOrder()
    {
        await using var fixture = new SolarWinEfSqliteRegressionFixture();
        var accountId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        await fixture.PrepareAsync(accountId);

        await using (var db = fixture.CreateDbContext(accountId))
        {
            var now = DateTimeOffset.UtcNow;
            db.Messages.AddRange(
                NewMessage(
                    roomId,
                    oldId,
                    new DateTimeOffset(
                        now.AddDays(-91).DateTime,
                        TimeSpan.FromHours(8))),
                NewMessage(
                    roomId,
                    newId,
                    new DateTimeOffset(
                        now.AddDays(-89).DateTime,
                        TimeSpan.FromHours(-7))));
            await db.SaveChangesAsync();
        }

        await using var pump = new ChatWritePump(fixture);
        pump.EnsureRunning();
        await pump.CompleteAndDrainAsync();

        await using var after = fixture.CreateDbContext(accountId);
        var ids = await after.Messages.AsNoTracking().Select(message => message.MessageId).ToListAsync();
        Assert.DoesNotContain(oldId, ids);
        Assert.Contains(newId, ids);
    }

    [Fact]
    public async Task ChatWritePump_RetentionEnforcesCountCapAfterAgePass()
    {
        await using var fixture = new SolarWinEfSqliteRegressionFixture();
        var account = Guid.NewGuid();
        var room = Guid.NewGuid();
        await fixture.PrepareAsync(account);
        await using (var db = fixture.CreateDbContext(account))
        {
            var now = DateTimeOffset.UtcNow;
            for (var i = 1; i <= ChatWritePump.MaxMessagesPerRoom + 3; i++)
            {
                var row = NewMessage(room, Guid.NewGuid(), now);
                row.RoomSequence = i;
                db.Messages.Add(row);
            }

            await db.SaveChangesAsync();
        }

        await using var pump = new ChatWritePump(fixture);
        pump.EnsureRunning();
        await pump.CompleteAndDrainAsync();

        await using var after = fixture.CreateDbContext(account);
        Assert.Equal(0, pump.BatchesFailed);
        Assert.Equal(ChatWritePump.MaxMessagesPerRoom, await after.Messages.CountAsync());
        Assert.Equal(4, await after.Messages.MinAsync(row => row.RoomSequence));
    }

    [Fact]
    public async Task LegacyImport_AbortsWhenExpectedAccountIsSwitchedDuringReadyWait()
    {
        await using var fixture = new SolarWinEfSqliteRegressionFixture();
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        await fixture.PrepareAsync(accountA);
        await fixture.PrepareAsync(accountB);
        fixture.Bind(accountA);

        var cacheKey = $"chat_rooms_{accountA:N}";
        OfflineCache.SetJson(cacheKey, new List<SnChatRoom>
        {
            new() { Id = Guid.NewGuid(), Name = "account A" },
        });

        try
        {
            var readyEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.BlockReady(readyEntered);
            var import = new RoomLocalStore(fixture).TryImportLegacyJsonAsync(accountA);
            await readyEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            fixture.Switch(accountB);

            Assert.False(await import.WaitAsync(TimeSpan.FromSeconds(2)));
            await using var accountBDb = fixture.CreateDbContext(accountB);
            Assert.Empty(await accountBDb.Rooms.AsNoTracking().ToListAsync());
            Assert.True(OfflineCache.TryGetJson<List<SnChatRoom>>(cacheKey, out var retained));
            Assert.Equal("account A", Assert.Single(retained!).Name);
        }
        finally
        {
            OfflineCache.Remove(cacheKey);
        }
    }

    private static ChatRoomEntity NewRoom(Guid id, DateTimeOffset lastActivity)
        => new()
        {
            RoomId = id,
            LastActivity = lastActivity,
            SyncedAt = DateTimeOffset.UtcNow,
            Source = ChatMessageSource.Api,
        };

    private static ChatMessageEntity NewMessage(Guid roomId, Guid messageId, DateTimeOffset createdAt)
        => new()
        {
            RowId = Guid.NewGuid(),
            RoomId = roomId,
            MessageId = messageId,
            RoomSequence = messageId.GetHashCode(),
            CreatedAt = createdAt,
            SyncedAt = DateTimeOffset.UtcNow,
            Type = "text",
            Content = messageId.ToString("N"),
            SenderId = Guid.NewGuid(),
            Source = ChatMessageSource.Api,
        };

    private static SnChatMessage Message(Guid roomId, Guid messageId, string content)
        => new()
        {
            ChatRoomId = roomId,
            Id = messageId,
            RoomSequence = 1,
            Type = "text",
            Content = content,
            SenderId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static async Task SeedMessageAsync(
        SolarWinEfSqliteRegressionFixture fixture,
        Guid roomId,
        Guid messageId,
        string content)
    {
        await using var db = fixture.CreateDbContext();
        db.Messages.Add(new ChatMessageEntity
        {
            RowId = Guid.NewGuid(),
            RoomId = roomId,
            MessageId = messageId,
            RoomSequence = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            SyncedAt = DateTimeOffset.UtcNow,
            Type = "text",
            Content = content,
            SenderId = Guid.NewGuid(),
            Source = ChatMessageSource.Api,
        });
        await db.SaveChangesAsync();
    }
}
