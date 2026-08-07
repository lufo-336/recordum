using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkoutLogUnitV216 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 8, 8, 0, 0, 0, 0, DateTimeKind.Utc), "2.16" });

            migrationBuilder.UpdateData(
                table: "habit_templates",
                keyColumn: "id",
                keyValue: new Guid("00000003-0000-0000-0000-000000000002"),
                column: "payload_json",
                value: "{\"node\":{\"name\":\"Workout log\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#e85d4a\"},\"config\":{\"schedule_type\":\"SpecificDays\",\"streak_mode\":\"Weekly\",\"streak_recovery\":\"None\",\"has_integrated_timer\":true,\"timer_mode\":\"Session\"},\"metrics\":[{\"name\":\"Duration\",\"type\":\"Seconds\",\"unit\":\"minutes\"},{\"name\":\"Reps\",\"type\":\"Integer\"},{\"name\":\"Weight\",\"type\":\"Decimal\"}],\"segments\":[{\"name\":\"Work\",\"is_rest\":false,\"sort_order\":0},{\"name\":\"Rest\",\"is_rest\":true,\"sort_order\":1}]}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Utc), "2.15" });

            migrationBuilder.UpdateData(
                table: "habit_templates",
                keyColumn: "id",
                keyValue: new Guid("00000003-0000-0000-0000-000000000002"),
                column: "payload_json",
                value: "{\"node\":{\"name\":\"Workout log\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#e85d4a\"},\"config\":{\"schedule_type\":\"SpecificDays\",\"streak_mode\":\"Weekly\",\"streak_recovery\":\"None\",\"has_integrated_timer\":true,\"timer_mode\":\"Session\"},\"metrics\":[{\"name\":\"Duration\",\"type\":\"Seconds\"},{\"name\":\"Reps\",\"type\":\"Integer\"},{\"name\":\"Weight\",\"type\":\"Decimal\"}],\"segments\":[{\"name\":\"Work\",\"is_rest\":false,\"sort_order\":0},{\"name\":\"Rest\",\"is_rest\":true,\"sort_order\":1}]}");
        }
    }
}
