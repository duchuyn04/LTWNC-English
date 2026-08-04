using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountVerificationAndGoogleAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "AppUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GoogleSubjectId",
                table: "AppUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailOtpChallenges",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PendingRegistrationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    GoogleSubjectId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CodeHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    RequestIpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOtpChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingRegistrations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingRegistrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "AppUserGoogleSubjectIndex",
                table: "AppUsers",
                column: "GoogleSubjectId",
                unique: true,
                filter: "[GoogleSubjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOtpChallenges_NormalizedEmail_CreatedAtUtc",
                table: "EmailOtpChallenges",
                columns: new[] { "NormalizedEmail", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOtpChallenges_RequestIpAddress_CreatedAtUtc",
                table: "EmailOtpChallenges",
                columns: new[] { "RequestIpAddress", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_NormalizedEmail",
                table: "PendingRegistrations",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_NormalizedUserName",
                table: "PendingRegistrations",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailOtpChallenges");

            migrationBuilder.DropTable(
                name: "PendingRegistrations");

            migrationBuilder.DropIndex(
                name: "AppUserGoogleSubjectIndex",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "GoogleSubjectId",
                table: "AppUsers");
        }
    }
}
