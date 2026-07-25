using Microsoft.EntityFrameworkCore;
using SolarWin.Data.Entities;

namespace SolarWin.Data;

/// <summary>Per-account SQLite context (one file per accountId; design v1.0).</summary>
public sealed class SolarWinDbContext : DbContext
{
    public SolarWinDbContext(DbContextOptions<SolarWinDbContext> options)
        : base(options)
    {
    }

    public DbSet<ChatMessageEntity> Messages => Set<ChatMessageEntity>();

    public DbSet<ChatRoomEntity> Rooms => Set<ChatRoomEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureMessages(modelBuilder);
        ConfigureRooms(modelBuilder);
    }

    private static void ConfigureMessages(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<ChatMessageEntity>();

        e.ToTable("chat_messages");

        e.HasKey(x => x.RowId);

        e.Property(x => x.RowId).IsRequired();
        e.Property(x => x.MessageId).IsRequired();
        e.Property(x => x.RoomId).IsRequired();
        e.Property(x => x.RoomSequence).IsRequired();
        e.Property(x => x.Type).HasMaxLength(64);
        e.Property(x => x.Content);
        e.Property(x => x.SenderId).IsRequired();
        e.Property(x => x.ClientMessageId).HasMaxLength(128);
        e.Property(x => x.IsEncrypted).IsRequired();
        e.Property(x => x.RepliedMessageId);
        e.Property(x => x.ForwardedMessageId);
        e.Property(x => x.Ciphertext);
        e.Property(x => x.EncryptionHeader);
        e.Property(x => x.EncryptionSignature);
        e.Property(x => x.EncryptionScheme).HasMaxLength(64);
        e.Property(x => x.EncryptionEpoch);
        e.Property(x => x.EncryptionMessageType).HasMaxLength(64);
        e.Property(x => x.Nonce).HasMaxLength(128);
        e.Property(x => x.PayloadJson);
        e.Property(x => x.SyncedAt).IsRequired();
        e.Property(x => x.Source).IsRequired().HasConversion<int>();

        // 1) Keyset pagination (design v1.0 §2)
        e.HasIndex(x => new { x.RoomId, x.RoomSequence, x.MessageId })
            .HasDatabaseName("IX_messages_room_seq_id")
            .IsDescending(false, true, true);

        // 2) Server id uniqueness; Empty MessageId excluded so optimistic rows can coexist
        //    (SQLite UNIQUE allows multiple NULLs; Empty is not NULL — use filter.)
        e.HasIndex(x => new { x.RoomId, x.MessageId })
            .IsUnique()
            .HasDatabaseName("UQ_messages_room_msgid")
            .HasFilter("MessageId != '00000000-0000-0000-0000-000000000000'");

        // 3) Client echo id; NULL/empty not indexed as unique (SQLite multi-NULL OK; filter empties)
        e.HasIndex(x => new { x.RoomId, x.ClientMessageId })
            .IsUnique()
            .HasDatabaseName("UQ_messages_room_client")
            .HasFilter("ClientMessageId IS NOT NULL AND ClientMessageId != ''");

        // 4) Eviction / age scans (design v1.0 §1.7)
        e.HasIndex(x => new { x.RoomId, x.CreatedAt })
            .HasDatabaseName("IX_messages_room_created")
            .IsDescending(false, true);
    }

    private static void ConfigureRooms(ModelBuilder modelBuilder)
    {
        var e = modelBuilder.Entity<ChatRoomEntity>();

        e.ToTable("chat_rooms");
        e.HasKey(x => x.RoomId);

        e.Property(x => x.RoomId).IsRequired();
        e.Property(x => x.Type).IsRequired();
        e.Property(x => x.Pinned).IsRequired();
        e.Property(x => x.LastActivity).IsRequired();
        e.Property(x => x.UnreadCount).IsRequired();
        e.Property(x => x.LastReadSequence).IsRequired();
        e.Property(x => x.Name).HasMaxLength(256);
        e.Property(x => x.AvatarFileId).HasMaxLength(128);
        e.Property(x => x.LastMessageType).HasMaxLength(64);
        e.Property(x => x.PreviewPlaintext).HasMaxLength(256);
        e.Property(x => x.PreviewEncryptionScheme).HasMaxLength(64);
        e.Property(x => x.PreviewNonce).HasMaxLength(128);
        e.Property(x => x.PayloadJson);
        e.Property(x => x.SyncedAt).IsRequired();
        e.Property(x => x.Source).IsRequired().HasConversion<int>();

        // Phase 6: sole list index — ORDER BY Pinned DESC, LastActivity DESC
        e.HasIndex(x => new { x.Pinned, x.LastActivity })
            .HasDatabaseName("IX_chat_rooms_list")
            .IsDescending(true, true);
    }
}
