using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ltwnc.Data;

#nullable disable

namespace ltwnc.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260719093000_AddAdminDashboardKpis")]
    public partial class AddAdminDashboardKpis : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiOperationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProviderId = table.Column<int>(type: "int", nullable: true),
                    ProviderName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ModelId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    FailureKind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiOperationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiOperationLogs_OccurredAtUtc",
                table: "AiOperationLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiOperationLogs_OccurredAtUtc_Succeeded",
                table: "AiOperationLogs",
                columns: new[] { "OccurredAtUtc", "Succeeded" });

            migrationBuilder.CreateIndex(
                name: "IX_AiOperationLogs_ProviderId_OccurredAtUtc",
                table: "AiOperationLogs",
                columns: new[] { "ProviderId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishMissions_CreatedAt",
                table: "EnglishMissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_StartedAt",
                table: "StudySessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_CreatedAt",
                table: "UserProfiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserProgresses_LastReviewed",
                table: "UserProgresses",
                column: "LastReviewed");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EnglishMissions_CreatedAt",
                table: "EnglishMissions");

            migrationBuilder.DropIndex(
                name: "IX_StudySessions_StartedAt",
                table: "StudySessions");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_CreatedAt",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProgresses_LastReviewed",
                table: "UserProgresses");

            migrationBuilder.DropTable(
                name: "AiOperationLogs");
        }
    }
}
