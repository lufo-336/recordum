using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class CounterReclassV210 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Spec multivalue §5: ex-"series" built from the old builder (resistance with a plain
            // measured metric) become counters; pure-abstinence resistances (no measured metric)
            // and scale-only resistances (labels_json/scale_template_id set) stay.
            migrationBuilder.Sql("""
                UPDATE habit_nodes SET habit_type = 'Counter'
                WHERE habit_type = 'Resistance'
                  AND id IN (SELECT habit_node_id FROM metric_definitions
                             WHERE scale_template_id IS NULL
                               AND labels_json IS NULL
                               AND type IN ('Integer','Decimal','Seconds'));
                """);

            migrationBuilder.Sql(
                "UPDATE app_settings SET value = '2.10', updated_at = '2026-06-13T00:00:00Z' WHERE key = 'schema_version';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The reclass is not reversible without loss (we no longer know which counters were
            // resistances). Only the schema_version is rolled back.
            migrationBuilder.Sql(
                "UPDATE app_settings SET value = '2.9', updated_at = '2026-06-05T00:00:00Z' WHERE key = 'schema_version';");
        }
    }
}
