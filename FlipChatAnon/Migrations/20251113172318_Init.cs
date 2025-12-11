using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FlipChatAnon.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    connection_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    age_group = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    preferred_gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    preferred_age = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    theme = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "light"),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.connection_id);
                    table.CheckConstraint("ck_user_sessions_age", "age_group IN ('under14', '15-17', '18plus')");
                    table.CheckConstraint("ck_user_sessions_gender", "gender IN ('male', 'female', 'other')");
                    table.CheckConstraint("ck_user_sessions_pref_age", "preferred_age IN ('under14', '15-17', '18plus')");
                    table.CheckConstraint("ck_user_sessions_pref_gender", "preferred_gender IN ('male', 'female', 'any')");
                    table.CheckConstraint("ck_user_sessions_theme", "theme IN ('light', 'dark')");
                });

            migrationBuilder.CreateTable(
                name: "chat_rooms",
                columns: table => new
                {
                    room_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    user1_connection_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    user2_connection_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_rooms", x => x.room_id);
                    table.ForeignKey(
                        name: "FK_chat_rooms_user_sessions_user1_connection_id",
                        column: x => x.user1_connection_id,
                        principalTable: "user_sessions",
                        principalColumn: "connection_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_rooms_user_sessions_user2_connection_id",
                        column: x => x.user2_connection_id,
                        principalTable: "user_sessions",
                        principalColumn: "connection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    message_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    room_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    message_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sender_connection_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.message_id);
                    table.ForeignKey(
                        name: "FK_messages_chat_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "chat_rooms",
                        principalColumn: "room_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_messages_user_sessions_sender_connection_id",
                        column: x => x.sender_connection_id,
                        principalTable: "user_sessions",
                        principalColumn: "connection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chat_rooms_created_at",
                table: "chat_rooms",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_chat_rooms_user1_unique",
                table: "chat_rooms",
                column: "user1_connection_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_rooms_user2_unique",
                table: "chat_rooms",
                column: "user2_connection_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_messages_created_at",
                table: "messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_messages_room_created",
                table: "messages",
                columns: new[] { "room_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_messages_room_id",
                table: "messages",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_sender",
                table: "messages",
                column: "sender_connection_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_gender_age",
                table: "user_sessions",
                columns: new[] { "gender", "age_group" });

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_joined_at",
                table: "user_sessions",
                column: "joined_at");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_preferences",
                table: "user_sessions",
                columns: new[] { "preferred_gender", "preferred_age" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "chat_rooms");

            migrationBuilder.DropTable(
                name: "user_sessions");
        }
    }
}
