using System;
using ltwnc.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260719000000_AddStudySessionTiming")]
public partial class AddStudySessionTiming : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "StartedAt",
            table: "StudySessions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DurationSeconds",
            table: "StudySessions",
            type: "int",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE [StudySessions] SET [StartedAt] = [CompletedAt] WHERE [StartedAt] IS NULL;");

        migrationBuilder.AlterColumn<DateTime>(
            name: "StartedAt",
            table: "StudySessions",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CompletedAt",
            table: "StudySessions",
            type: "datetime2",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "datetime2");

        migrationBuilder.CreateIndex(
            name: "IX_StudySessions_CompletedAt_UserId",
            table: "StudySessions",
            columns: new[] { "CompletedAt", "UserId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserProfiles_IsPublic_ShowStats",
            table: "UserProfiles",
            columns: new[] { "IsPublic", "ShowStats" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_StudySessions_CompletedAt_UserId",
            table: "StudySessions");

        migrationBuilder.DropIndex(
            name: "IX_UserProfiles_IsPublic_ShowStats",
            table: "UserProfiles");

        migrationBuilder.Sql(
            "UPDATE [StudySessions] SET [CompletedAt] = [StartedAt] WHERE [CompletedAt] IS NULL;");

        migrationBuilder.AlterColumn<DateTime>(
            name: "CompletedAt",
            table: "StudySessions",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "DurationSeconds",
            table: "StudySessions");

        migrationBuilder.DropColumn(
            name: "StartedAt",
            table: "StudySessions");
    }
}
