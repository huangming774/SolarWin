using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarWin.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_rooms",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Pinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastActivity = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UnreadCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastReadSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsCommunity = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    EncryptionMode = table.Column<int>(type: "INTEGER", nullable: false),
                    AvatarFileId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LastMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastMessageSequence = table.Column<long>(type: "INTEGER", nullable: true),
                    LastMessageType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PreviewIsEncrypted = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreviewCiphertext = table.Column<byte[]>(type: "BLOB", nullable: true),
                    PreviewEncryptionHeader = table.Column<byte[]>(type: "BLOB", nullable: true),
                    PreviewEncryptionSignature = table.Column<byte[]>(type: "BLOB", nullable: true),
                    PreviewEncryptionScheme = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PreviewEncryptionEpoch = table.Column<long>(type: "INTEGER", nullable: true),
                    PreviewNonce = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PreviewPlaintext = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_rooms", x => x.RoomId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_rooms_list",
                table: "chat_rooms",
                columns: new[] { "Pinned", "LastActivity" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_rooms");
        }
    }
}
