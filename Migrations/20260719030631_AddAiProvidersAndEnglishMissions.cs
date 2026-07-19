using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddAiProvidersAndEnglishMissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProviders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AdapterType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ModelId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiKeyLastFour = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCheckSucceeded = table.Column<bool>(type: "bit", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnglishMissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudySessionId = table.Column<int>(type: "int", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Situation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NpcName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NpcRole = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OpeningLine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GoalsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TurnCount = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishMissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishMissions_StudySessions_StudySessionId",
                        column: x => x.StudySessionId,
                        principalTable: "StudySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishMissionTargetWords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnglishMissionId = table.Column<int>(type: "int", nullable: false),
                    FlashcardId = table.Column<int>(type: "int", nullable: false),
                    Term = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Definition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PartOfSpeech = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ExampleSentence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    FirstUsedTurn = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishMissionTargetWords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishMissionTargetWords_EnglishMissions_EnglishMissionId",
                        column: x => x.EnglishMissionId,
                        principalTable: "EnglishMissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishMissionTargetWords_Flashcards_FlashcardId",
                        column: x => x.FlashcardId,
                        principalTable: "Flashcards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnglishMissionTurns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnglishMissionId = table.Column<int>(type: "int", nullable: false),
                    TurnNumber = table.Column<int>(type: "int", nullable: false),
                    UserText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NpcText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FeedbackVi = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrectionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrectionExplanationVi = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UsedWordsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AchievedGoalsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ModelId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishMissionTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishMissionTurns_EnglishMissions_EnglishMissionId",
                        column: x => x.EnglishMissionId,
                        principalTable: "EnglishMissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviders_IsEnabled_Priority",
                table: "AiProviders",
                columns: new[] { "IsEnabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishMissions_StudySessionId",
                table: "EnglishMissions",
                column: "StudySessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnglishMissionTargetWords_EnglishMissionId",
                table: "EnglishMissionTargetWords",
                column: "EnglishMissionId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishMissionTargetWords_FlashcardId",
                table: "EnglishMissionTargetWords",
                column: "FlashcardId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishMissionTurns_EnglishMissionId",
                table: "EnglishMissionTurns",
                column: "EnglishMissionId");

            migrationBuilder.Sql(@"
                INSERT INTO [AiProviders]
                    ([Name], [AdapterType], [BaseUrl], [ModelId], [EncryptedApiKey], [ApiKeyLastFour], [IsEnabled], [Priority], [TimeoutSeconds], [LastCheckedAt], [LastCheckSucceeded], [LastError], [CreatedAt], [UpdatedAt])
                VALUES
                    (N'9Router Local', N'OpenAICompatible', N'http://localhost:20128/v1', N'cx/gpt-5.6-luna', NULL, NULL, 1, 1, 60, NULL, NULL, NULL, SYSUTCDATETIME(), SYSUTCDATETIME());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProviders");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "EnglishMissionTargetWords");

            migrationBuilder.DropTable(
                name: "EnglishMissionTurns");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "EnglishMissions");
        }
    }
}
