using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrikeShield.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingAndIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnScanCompletion = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnCriticalFinding = table.Column<bool>(type: "boolean", nullable: false),
                    WebhookUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GitHubRepository = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    GitHubAccessToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Integrations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScanSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EngagementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaybookId = table.Column<Guid>(type: "uuid", nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastFiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFireOutcome = table.Column<int>(type: "integer", nullable: true),
                    LastSkipReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastTriggeredScanJobId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanSchedules_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScanSchedules_Playbooks_PlaybookId",
                        column: x => x.PlaybookId,
                        principalTable: "Playbooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanSchedules_Targets_TargetId",
                        column: x => x.TargetId,
                        principalTable: "Targets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_OrganizationId",
                table: "Integrations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanSchedules_EngagementId",
                table: "ScanSchedules",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanSchedules_PlaybookId",
                table: "ScanSchedules",
                column: "PlaybookId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanSchedules_TargetId",
                table: "ScanSchedules",
                column: "TargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ScanSchedules");

            migrationBuilder.DropTable(name: "Integrations");
        }
    }
}
