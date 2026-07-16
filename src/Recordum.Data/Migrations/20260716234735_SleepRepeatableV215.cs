using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class SleepRepeatableV215 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                keyValue: new Guid("00000003-0000-0000-0000-000000000005"),
                column: "payload_json",
                value: "{\"node\":{\"name\":\"Sleep tracker\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7b68ee\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\",\"repeatable\":true,\"series_labels\":[\"Night\",\"Nap\"]},\"metrics\":[{\"name\":\"Hours slept\",\"type\":\"Decimal\",\"unit\":\"hours\"},{\"name\":\"Sleep quality\",\"type\":\"Integer\",\"scale_code\":\"sleep-quality\"}],\"segments\":[]}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "key",
                keyValue: "schema_version",
                columns: new[] { "updated_at", "value" },
                values: new object[] { new DateTime(2026, 7, 9, 0, 0, 0, 0, DateTimeKind.Utc), "2.14" });

            migrationBuilder.UpdateData(
                table: "habit_templates",
                keyColumn: "id",
                keyValue: new Guid("00000003-0000-0000-0000-000000000005"),
                column: "payload_json",
                value: "{\"node\":{\"name\":\"Sleep tracker\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7b68ee\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Hours slept\",\"type\":\"Decimal\",\"unit\":\"hours\"},{\"name\":\"Sleep quality\",\"type\":\"Integer\",\"scale_code\":\"sleep-quality\"}],\"segments\":[]}");
        }
    }
}
