using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditsAndSePayPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreditBalance",
                table: "AppUsers",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "CreditVersion",
                table: "AppUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CreditLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AdminActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditLedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_CreditLedgerEntries_BalanceAfter", "[BalanceAfter] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "CreditPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PriceVnd = table.Column<long>(type: "bigint", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPackages", x => x.Id);
                    table.CheckConstraint("CK_CreditPackages_Credits", "[Credits] > 0");
                    table.CheckConstraint("CK_CreditPackages_PriceVnd", "[PriceVnd] > 0");
                });

            migrationBuilder.CreateTable(
                name: "CreditPurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreditPackageId = table.Column<int>(type: "int", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PackageName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PriceVnd = table.Column<long>(type: "bigint", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SePayOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SePayTransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_CreditPackages_CreditPackageId",
                        column: x => x.CreditPackageId,
                        principalTable: "CreditPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgerEntries_SourceType_SourceId",
                table: "CreditLedgerEntries",
                columns: new[] { "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgerEntries_UserId_CreatedAtUtc",
                table: "CreditLedgerEntries",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditPackages_IsArchived_IsActive_DisplayOrder",
                table: "CreditPackages",
                columns: new[] { "IsArchived", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_CreditPackageId",
                table: "CreditPurchases",
                column: "CreditPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_InvoiceNumber",
                table: "CreditPurchases",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_SePayTransactionId",
                table: "CreditPurchases",
                column: "SePayTransactionId",
                unique: true,
                filter: "[SePayTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_UserId_Status_ExpiresAtUtc",
                table: "CreditPurchases",
                columns: new[] { "UserId", "Status", "ExpiresAtUtc" });

            migrationBuilder.Sql("""
                INSERT INTO CreditPackages
                    (Name, Description, PriceVnd, Credits, DisplayOrder, IsActive, IsArchived, Version, CreatedAtUtc, UpdatedAtUtc)
                VALUES
                    (N'Cơ bản', N'25 lượt chat English Mission', 20000, 25, 1, 1, 0, 0, GETUTCDATE(), GETUTCDATE()),
                    (N'Tiêu chuẩn', N'75 lượt chat English Mission', 50000, 75, 2, 1, 0, 0, GETUTCDATE(), GETUTCDATE()),
                    (N'Nâng cao', N'180 lượt chat English Mission', 100000, 180, 3, 1, 0, 0, GETUTCDATE(), GETUTCDATE());

                INSERT INTO CreditLedgerEntries
                    (UserId, Amount, BalanceAfter, Type, SourceType, SourceId, Description, CreatedAtUtc)
                SELECT Id, 10, 10, 'WelcomeBonus', 'UserRegistration', Id,
                       N'Tín dụng chào mừng', GETUTCDATE()
                FROM AppUsers;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditLedgerEntries");

            migrationBuilder.DropTable(
                name: "CreditPurchases");

            migrationBuilder.DropTable(
                name: "CreditPackages");

            migrationBuilder.DropColumn(
                name: "CreditBalance",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "CreditVersion",
                table: "AppUsers");
        }
    }
}
