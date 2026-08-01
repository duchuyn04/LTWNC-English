using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class EnforceCreditIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_UserId",
                table: "CreditPurchases",
                column: "UserId",
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditLedgerEntries_AppUsers_UserId",
                table: "CreditLedgerEntries",
                column: "UserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditPurchases_AppUsers_UserId",
                table: "CreditPurchases",
                column: "UserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditLedgerEntries_AppUsers_UserId",
                table: "CreditLedgerEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditPurchases_AppUsers_UserId",
                table: "CreditPurchases");

            migrationBuilder.DropIndex(
                name: "IX_CreditPurchases_UserId",
                table: "CreditPurchases");
        }
    }
}
