using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarWin.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    RowId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    SenderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientMessageId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IsEncrypted = table.Column<bool>(type: "INTEGER", nullable: false),
                    RepliedMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ForwardedMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Ciphertext = table.Column<byte[]>(type: "BLOB", nullable: true),
                    EncryptionHeader = table.Column<byte[]>(type: "BLOB", nullable: true),
                    EncryptionSignature = table.Column<byte[]>(type: "BLOB", nullable: true),
                    EncryptionScheme = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EncryptionEpoch = table.Column<long>(type: "INTEGER", nullable: true),
                    EncryptionMessageType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Nonce = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.RowId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_messages_room_created",
                table: "chat_messages",
                columns: new[] { "RoomId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_messages_room_seq_id",
                table: "chat_messages",
                columns: new[] { "RoomId", "RoomSequence", "MessageId" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "UQ_messages_room_client",
                table: "chat_messages",
                columns: new[] { "RoomId", "ClientMessageId" },
                unique: true,
                filter: "ClientMessageId IS NOT NULL AND ClientMessageId != ''");

            migrationBuilder.CreateIndex(
                name: "UQ_messages_room_msgid",
                table: "chat_messages",
                columns: new[] { "RoomId", "MessageId" },
                unique: true,
                filter: "MessageId != '00000000-0000-0000-0000-000000000000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages");
        }
    }
}
