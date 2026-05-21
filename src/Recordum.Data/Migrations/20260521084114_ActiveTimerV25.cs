using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class ActiveTimerV25 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "active_timer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    habit_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    started_at_ms = table.Column<long>(type: "INTEGER", nullable: false),
                    timer_mode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    segment_index = table.Column<int>(type: "INTEGER", nullable: true),
                    paused_at_ms = table.Column<long>(type: "INTEGER", nullable: true),
                    accumulated_paused_ms = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_active_timer", x => x.id);
                    table.CheckConstraint("ck_active_timer_mode", "timer_mode IN ('Stopwatch','Countdown','Primary','Session')");
                    table.ForeignKey(
                        name: "fk_active_timer_habit_nodes_habit_node_id",
                        column: x => x.habit_node_id,
                        principalTable: "habit_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_active_timer_habit_node_id",
                table: "active_timer",
                column: "habit_node_id",
                unique: true);

            migrationBuilder.Sql(
                "UPDATE app_settings SET value = '2.5' WHERE key = 'schema_version';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE app_settings SET value = '2.4' WHERE key = 'schema_version';");

            migrationBuilder.DropTable(
                name: "active_timer");
        }
    }
}
