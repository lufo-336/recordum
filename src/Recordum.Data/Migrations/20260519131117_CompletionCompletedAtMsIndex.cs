using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF-generated array literal

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompletionCompletedAtMsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_completions_habit_node_id_completed_at_ms",
                table: "completions",
                columns: new[] { "habit_node_id", "completed_at_ms" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_completions_habit_node_id_completed_at_ms",
                table: "completions");
        }
    }
}
