using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class PinScopeV24 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add pin_scope with default 'None' while is_pinned still exists.
            migrationBuilder.AddColumn<string>(
                name: "pin_scope",
                table: "habit_nodes",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");

            // Step 2: Migrate legacy is_pinned = 1 rows to PinScope.Local BEFORE the table rebuild.
            // The SQLite provider generates a table-rebuild to drop is_pinned, so pin_scope must
            // be populated here while the old column is still present and queryable.
            migrationBuilder.Sql(
                "UPDATE habit_nodes SET pin_scope = 'Local' WHERE is_pinned = 1;");

            // Step 3: Drop is_pinned (triggers SQLite table-rebuild; the INSERT...SELECT carries
            // the already-populated pin_scope value, preserving the mapping above).
            migrationBuilder.DropColumn(
                name: "is_pinned",
                table: "habit_nodes");

            migrationBuilder.CreateIndex(
                name: "ix_habit_nodes_pin_scope",
                table: "habit_nodes",
                column: "pin_scope");

            migrationBuilder.AddCheckConstraint(
                name: "ck_habit_node_pin_scope",
                table: "habit_nodes",
                sql: "pin_scope IN ('None','Local','Global')");

            // Step 4: Bump schema_version in app_settings.
            // The AppSettingSeed uses HasData (seeded during InitialSchema), so the row already
            // exists. We update it directly here for databases that are being migrated.
            migrationBuilder.Sql(
                "UPDATE app_settings SET value = '2.4' WHERE key = 'schema_version';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_habit_nodes_pin_scope",
                table: "habit_nodes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_habit_node_pin_scope",
                table: "habit_nodes");

            migrationBuilder.DropColumn(
                name: "pin_scope",
                table: "habit_nodes");

            migrationBuilder.AddColumn<bool>(
                name: "is_pinned",
                table: "habit_nodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
