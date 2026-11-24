using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Recordum.Data.Sync;

#nullable disable
#pragma warning disable CA1861

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncFieldMsV221 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_field_ms",
                columns: table => new
                {
                    entity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    entity_id = table.Column<string>(type: "TEXT", nullable: false),
                    field = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_field_ms", x => new { x.entity, x.entity_id, x.field });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var entity in SyncCatalog.Entities)
            {
                var table = entity.Table.Replace("\"", "\"\"", StringComparison.Ordinal);
                migrationBuilder.Sql($"""
                    DROP TRIGGER IF EXISTS "trg_sync_del_{table}";
                    DROP TRIGGER IF EXISTS "trg_sync_upd_{table}";
                    DROP TRIGGER IF EXISTS "trg_sync_ins_{table}";
                    DROP TRIGGER IF EXISTS "trg_sync_fms_{table}";
                    """);
            }

            migrationBuilder.DropTable(
                name: "sync_field_ms");
        }
    }
}
