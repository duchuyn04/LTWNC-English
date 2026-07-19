using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddEnglishMissionTurnClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientTurnId",
                table: "EnglishMissionTurns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE [EnglishMissionTurns]
                SET [ClientTurnId] = CONVERT(nvarchar(64), NEWID())
                WHERE [ClientTurnId] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "ClientTurnId",
                table: "EnglishMissionTurns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnglishMissionTurns_EnglishMissionId_ClientTurnId",
                table: "EnglishMissionTurns",
                columns: new[] { "EnglishMissionId", "ClientTurnId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EnglishMissionTurns_EnglishMissionId_ClientTurnId",
                table: "EnglishMissionTurns");

            migrationBuilder.DropColumn(
                name: "ClientTurnId",
                table: "EnglishMissionTurns");
        }
    }
}
