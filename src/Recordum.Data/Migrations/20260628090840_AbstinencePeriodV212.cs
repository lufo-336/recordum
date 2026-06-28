using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class AbstinencePeriodV212 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "abstinence_period",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    started_at_ms = table.Column<long>(type: "INTEGER", nullable: false),
                    ended_at_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_abstinence_period", x => x.id);
                    table.ForeignKey(
                        name: "fk_abstinence_period_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 6, 28, 0, 0, 0, 0, DateTimeKind.Utc), "2.12" });

            migrationBuilder.CreateIndex(
                name: "ix_abstinence_period_habit_started",
                table: "abstinence_period",
                columns: new[] { "habit_node_id", "started_at_ms" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "abstinence_period");

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 6, 19, 0, 0, 0, 0, DateTimeKind.Utc), "2.11" });
        }
    }
}
