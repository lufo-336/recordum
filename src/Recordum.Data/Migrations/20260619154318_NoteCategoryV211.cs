using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class NoteCategoryV211 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "notes",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Veloce");

            migrationBuilder.AddColumn<Guid>(
                name: "completion_id",
                table: "notes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notes_completion_id",
                table: "notes",
                column: "completion_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_note_category",
                table: "notes",
                sql: "category IN ('Diario','Veloce')");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 6, 19, 0, 0, 0, 0, DateTimeKind.Utc), "2.11" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notes_completion_id",
                table: "notes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_note_category",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "category",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "completion_id",
                table: "notes");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 6, 13, 0, 0, 0, 0, DateTimeKind.Utc), "2.10" });
        }
    }
}
