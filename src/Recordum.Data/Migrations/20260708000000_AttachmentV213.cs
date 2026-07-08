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
            migrationBuilder.CreateTable(
                name: "attachment",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    note_id = table.Column<string>(type: "TEXT", nullable: false),
                    filename = table.Column<string>(type: "TEXT", nullable: false),
                    mime_type = table.Column<string>(type: "TEXT", nullable: false),
                    file_size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachment", x => x.id);
                    table.CheckConstraint("ck_attachment_size", "file_size_bytes >= 0");
                    table.ForeignKey(
                        name: "fk_attachment_note_id",
                        column: x => x.note_id,
                        principalTable: "note",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attachment_note_id",
                table: "attachment",
                column: "note_id");

            migrationBuilder.UpdateData(
                table: "app_setting",
                keyColumn: "key",
                keyValue: "schema_version",
                column: "value",
                value: "2.13");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment");

            migrationBuilder.UpdateData(
                table: "app_setting",
                keyColumn: "key",
                keyValue: "schema_version",
                column: "value",
                value: "2.12");
        }
    }
}
