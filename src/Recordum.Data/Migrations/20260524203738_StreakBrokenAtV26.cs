using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class StreakBrokenAtV26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "streak_broken_at",
                table: "habit_configs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE app_settings SET value = '2.6' WHERE key = 'schema_version';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE app_settings SET value = '2.5' WHERE key = 'schema_version';");

            migrationBuilder.DropColumn(
                name: "streak_broken_at",
                table: "habit_configs");
        }
    }
}
