using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    public partial class AddAiProviderPrimaryAndVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "AiProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "AiProviders",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_AiProviders_IsPrimary",
                table: "AiProviders",
                column: "IsPrimary",
                unique: true,
                filter: "[IsPrimary] = 1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiProviders_IsPrimary",
                table: "AiProviders");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "AiProviders");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AiProviders");
        }
    }
}
