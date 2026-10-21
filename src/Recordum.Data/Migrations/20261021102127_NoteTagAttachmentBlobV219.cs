using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class NoteTagAttachmentBlobV219 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "content",
                table: "attachment",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_missing",
                table: "attachment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "thumbnail",
                table: "attachment",
                type: "BLOB",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "note_tag",
                columns: table => new
                {
                    note_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tag_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_note_tag", x => new { x.note_id, x.tag_id, x.source });
                    table.CheckConstraint("ck_note_tag_source", "source IN ('Inline','Property')");
                    table.ForeignKey(
                        name: "fk_note_tag_note_id",
                        column: x => x.note_id,
                        principalTable: "notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_note_tag_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "2.19" });

            migrationBuilder.CreateIndex(
                name: "ix_note_tag_tag_id",
                table: "note_tag",
                column: "tag_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "note_tag");

            migrationBuilder.DropColumn(
                name: "content",
                table: "attachment");

            migrationBuilder.DropColumn(
                name: "is_missing",
                table: "attachment");

            migrationBuilder.DropColumn(
                name: "thumbnail",
                table: "attachment");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 9, 10, 0, 0, 0, 0, DateTimeKind.Utc), "2.18" });
        }
    }
}
