using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentV213 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drops the Milestone 3.1-era "attachments" (plural) table: a polymorphic
            // Note-or-Completion attachment scaffold from InitialSchema that was never wired to
            // any UI feature. Onda 1b (photo attachments) repurposes the concept as "attachment"
            // (singular) — a Note-only entity backing IAttachmentService — so this migration
            // both retires the dead scaffold and introduces the real table in one coherent step.
            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.CreateTable(
                name: "attachment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    note_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    filename = table.Column<string>(type: "TEXT", nullable: false),
                    mime_type = table.Column<string>(type: "TEXT", nullable: false),
                    file_size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachment", x => x.id);
                    table.CheckConstraint("ck_attachment_size", "file_size_bytes >= 0");
                    table.ForeignKey(
                        name: "fk_attachment_note_id",
                        column: x => x.note_id,
                        principalTable: "notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                column: "value",
                value: "2.13");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                column: "updated_at",
                value: new DateTime(2026, 7, 8, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.CreateIndex(
                name: "ix_attachment_note_id",
                table: "attachment",
                column: "note_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment");

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    completion_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    note_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    caption = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    file_path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.CheckConstraint("ck_attachment_exactly_one_parent", "(completion_id IS NOT NULL OR note_id IS NOT NULL) AND NOT (completion_id IS NOT NULL AND note_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_attachments_completions_completion_id",
                        column: x => x.completion_id,
                        principalTable: "completions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attachments_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                column: "value",
                value: "2.12");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                column: "updated_at",
                value: new DateTime(2026, 6, 28, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.CreateIndex(
                name: "ix_attachments_completion_id",
                table: "attachments",
                column: "completion_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_note_id",
                table: "attachments",
                column: "note_id");
        }
    }
}
