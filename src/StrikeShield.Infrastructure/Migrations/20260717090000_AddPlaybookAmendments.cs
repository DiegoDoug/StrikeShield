using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrikeShield.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybookAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaybookAmendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedByStepRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetPlaybookStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rationale = table.Column<string>(type: "text", nullable: false),
                    ProposedArgsTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybookAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybookAmendments_ScanJobs_ScanJobId",
                        column: x => x.ScanJobId,
                        principalTable: "ScanJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaybookAmendments_StepRuns_ProposedByStepRunId",
                        column: x => x.ProposedByStepRunId,
                        principalTable: "StepRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaybookAmendments_PlaybookSteps_TargetPlaybookStepId",
                        column: x => x.TargetPlaybookStepId,
                        principalTable: "PlaybookSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookAmendments_ScanJobId",
                table: "PlaybookAmendments",
                column: "ScanJobId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookAmendments_ProposedByStepRunId",
                table: "PlaybookAmendments",
                column: "ProposedByStepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookAmendments_TargetPlaybookStepId",
                table: "PlaybookAmendments",
                column: "TargetPlaybookStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PlaybookAmendments");
        }
    }
}
