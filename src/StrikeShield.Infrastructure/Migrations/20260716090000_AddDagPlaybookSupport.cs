using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrikeShield.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDagPlaybookSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StepKey",
                table: "PlaybookSteps",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string[]>(
                name: "DependsOn",
                table: "PlaybookSteps",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<int>(
                name: "Condition",
                table: "PlaybookSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookSteps_PlaybookId_StepKey",
                table: "PlaybookSteps",
                columns: new[] { "PlaybookId", "StepKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlaybookSteps_PlaybookId_StepKey",
                table: "PlaybookSteps");

            migrationBuilder.DropColumn(name: "Condition", table: "PlaybookSteps");
            migrationBuilder.DropColumn(name: "DependsOn", table: "PlaybookSteps");
            migrationBuilder.DropColumn(name: "StepKey", table: "PlaybookSteps");
        }
    }
}
