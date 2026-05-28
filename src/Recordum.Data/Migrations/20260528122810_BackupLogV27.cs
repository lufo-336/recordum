using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Prefer static readonly fields
#pragma warning disable CA1825 // Avoid zero-length array allocations

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackupLogV27 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "backup_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    schema_version = table.Column<string>(type: "TEXT", nullable: false),
                    app_version = table.Column<string>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backup_log", x => x.id);
                    table.CheckConstraint("ck_backup_log_kind", "kind IN ('Manual','Auto','PreRestore')");
                });

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), "2.7" });

            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "key", "updated_at", "value" },
                values: new object[,]
                {
                    { "backup.auto.enabled", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), "true" },
                    { "backup.auto.frequency_days", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), "7" },
                    { "backup.auto.last_run_utc", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                    { "backup.auto.path", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), "" },
                    { "backup.auto.retention_count", new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Utc), "8" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_backup_log_created_at",
                table: "backup_log",
                column: "created_at",
                descending: new[] { true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backup_log");

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "backup.auto.enabled");

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "backup.auto.frequency_days");

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "backup.auto.last_run_utc");

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "backup.auto.path");

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "backup.auto.retention_count");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 5, 14, 0, 0, 0, 0, DateTimeKind.Utc), "2.6" });
        }
    }
}