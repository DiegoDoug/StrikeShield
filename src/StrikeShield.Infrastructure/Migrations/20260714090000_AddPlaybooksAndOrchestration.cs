using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrikeShield.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybooksAndOrchestration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Playbooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playbooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlaybookSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaybookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    ToolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImageRepository = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ImageTag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ArgsTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    MemoryLimitBytes = table.Column<long>(type: "bigint", nullable: false),
                    NanoCpus = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybookSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybookSteps_Playbooks_PlaybookId",
                        column: x => x.PlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.DropColumn(
                name: "PlaybookName",
                table: "ScanJobs");

            migrationBuilder.AddColumn<Guid>(
                name: "PlaybookId",
                table: "ScanJobs",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.CreateTable(
                name: "StepRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaybookStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ContainerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExitCode = table.Column<long>(type: "bigint", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StepRuns_ScanJobs_ScanJobId",
                        column: x => x.ScanJobId,
                        principalTable: "ScanJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StepRuns_PlaybookSteps_PlaybookStepId",
                        column: x => x.PlaybookStepId,
                        principalTable: "PlaybookSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Artifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artifacts_StepRuns_StepRunId",
                        column: x => x.StepRunId,
                        principalTable: "StepRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Playbooks_Slug",
                table: "Playbooks",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaybookSteps_PlaybookId",
                table: "PlaybookSteps",
                column: "PlaybookId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobs_PlaybookId",
                table: "ScanJobs",
                column: "PlaybookId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScanJobs_Playbooks_PlaybookId",
                table: "ScanJobs",
                column: "PlaybookId",
                principalTable: "Playbooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_StepRuns_ScanJobId",
                table: "StepRuns",
                column: "ScanJobId");

            migrationBuilder.CreateIndex(
                name: "IX_StepRuns_PlaybookStepId",
                table: "StepRuns",
                column: "PlaybookStepId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_StepRunId",
                table: "Artifacts",
                column: "StepRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScanJobs_Playbooks_PlaybookId",
                table: "ScanJobs");

            migrationBuilder.DropTable(name: "Artifacts");
            migrationBuilder.DropTable(name: "StepRuns");
            migrationBuilder.DropTable(name: "PlaybookSteps");
            migrationBuilder.DropTable(name: "Playbooks");

            migrationBuilder.DropColumn(
                name: "PlaybookId",
                table: "ScanJobs");

            migrationBuilder.AddColumn<string>(
                name: "PlaybookName",
                table: "ScanJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: string.Empty);
        }
    }
}
