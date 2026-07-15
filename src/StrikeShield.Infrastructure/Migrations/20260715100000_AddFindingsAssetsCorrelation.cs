using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrikeShield.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFindingsAssetsCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorrelationGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrelationGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscoveredByStepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assets_Targets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "Targets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assets_StepRuns_DiscoveredByStepRunId",
                        column: x => x.DiscoveredByStepRunId,
                        principalTable: "StepRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Findings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceTool = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    CvssVector = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CvssScore = table.Column<double>(type: "double precision", nullable: true),
                    CweIds = table.Column<string[]>(type: "text[]", nullable: false),
                    CveIds = table.Column<string[]>(type: "text[]", nullable: false),
                    OwaspCategory = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MitreAttackTechniques = table.Column<string[]>(type: "text[]", nullable: false),
                    AffectedAsset = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PocCode = table.Column<string>(type: "text", nullable: true),
                    ReproSteps = table.Column<string[]>(type: "text[]", nullable: false),
                    RecommendedFix = table.Column<string>(type: "text", nullable: true),
                    VerificationSteps = table.Column<string[]>(type: "text[]", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DedupeFingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_ScanJobs_ScanJobId",
                        column: x => x.ScanJobId,
                        principalTable: "ScanJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Findings_StepRuns_StepRunId",
                        column: x => x.StepRunId,
                        principalTable: "StepRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Findings_CorrelationGroups_CorrelationGroupId",
                        column: x => x.CorrelationGroupId,
                        principalTable: "CorrelationGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FindingEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FindingEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FindingEvidence_Findings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "Findings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_TargetId",
                table: "Assets",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_DiscoveredByStepRunId",
                table: "Assets",
                column: "DiscoveredByStepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_ScanJobId",
                table: "Findings",
                column: "ScanJobId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_StepRunId",
                table: "Findings",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_CorrelationGroupId",
                table: "Findings",
                column: "CorrelationGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_DedupeFingerprint",
                table: "Findings",
                column: "DedupeFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_FindingEvidence_FindingId",
                table: "FindingEvidence",
                column: "FindingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FindingEvidence");
            migrationBuilder.DropTable(name: "Findings");
            migrationBuilder.DropTable(name: "Assets");
            migrationBuilder.DropTable(name: "CorrelationGroups");
        }
    }
}
