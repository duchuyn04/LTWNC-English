using ltwnc.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260731100000_AddReviewPolicySettings")]
public partial class AddReviewPolicySettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ReviewMaxIntervalDays",
            table: "UserStudySettings",
            type: "int",
            nullable: false,
            defaultValue: 30);

        migrationBuilder.AddColumn<int>(
            name: "ReviewSessionSize",
            table: "UserStudySettings",
            type: "int",
            nullable: false,
            defaultValue: 20);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ReviewMaxIntervalDays",
            table: "UserStudySettings");

        migrationBuilder.DropColumn(
            name: "ReviewSessionSize",
            table: "UserStudySettings");
    }
}
