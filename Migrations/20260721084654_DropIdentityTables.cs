using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ltwnc.Migrations
{
    /// <inheritdoc />
    public partial class DropIdentityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Giữ nguyên id và password hash để tài khoản Identity cũ tiếp tục đăng nhập được.
            migrationBuilder.Sql(
                """
                INSERT INTO [AppUsers] (
                    [Id], [Email], [NormalizedEmail], [UserName], [NormalizedUserName],
                    [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [LockoutEnd],
                    [AccessFailedCount], [IsAdmin])
                SELECT
                    [user].[Id],
                    COALESCE(NULLIF([user].[Email], N''), CONCAT([user].[Id], N'@legacy.local')),
                    COALESCE(NULLIF([user].[NormalizedEmail], N''), UPPER(COALESCE(NULLIF([user].[Email], N''), CONCAT([user].[Id], N'@legacy.local')))),
                    COALESCE(NULLIF([user].[UserName], N''), [user].[Id]),
                    COALESCE(NULLIF([user].[NormalizedUserName], N''), UPPER(COALESCE(NULLIF([user].[UserName], N''), [user].[Id]))),
                    COALESCE([user].[PasswordHash], N''),
                    COALESCE(NULLIF([user].[SecurityStamp], N''), CONVERT(nvarchar(36), NEWID())),
                    COALESCE(NULLIF([user].[ConcurrencyStamp], N''), CONVERT(nvarchar(36), NEWID())),
                    [user].[LockoutEnd],
                    [user].[AccessFailedCount],
                    CASE WHEN EXISTS (
                        SELECT 1
                        FROM [AspNetUserRoles] AS [userRole]
                        INNER JOIN [AspNetRoles] AS [role] ON [role].[Id] = [userRole].[RoleId]
                        WHERE [userRole].[UserId] = [user].[Id]
                          AND [role].[NormalizedName] = N'ADMIN'
                    ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
                FROM [AspNetUsers] AS [user]
                WHERE NOT EXISTS (
                    SELECT 1 FROM [AppUsers] AS [appUser] WHERE [appUser].[Id] = [user].[Id]
                );
                """);

            // Một số database đã mất các FK này từ migration auth cũ, nên chỉ xóa khi còn tồn tại.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FlashcardSets_AspNetUsers_UserId')
                    ALTER TABLE [FlashcardSets] DROP CONSTRAINT [FK_FlashcardSets_AspNetUsers_UserId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_StudySessions_AspNetUsers_UserId')
                    ALTER TABLE [StudySessions] DROP CONSTRAINT [FK_StudySessions_AspNetUsers_UserId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_UserProgresses_AspNetUsers_UserId')
                    ALTER TABLE [UserProgresses] DROP CONSTRAINT [FK_UserProgresses_AspNetUsers_UserId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_UserStudySettings_AspNetUsers_UserId')
                    ALTER TABLE [UserStudySettings] DROP CONSTRAINT [FK_UserStudySettings_AspNetUsers_UserId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_UserProfiles_AspNetUsers_UserId')
                    ALTER TABLE [UserProfiles] DROP CONSTRAINT [FK_UserProfiles_AspNetUsers_UserId];

                DROP TABLE IF EXISTS [AspNetRoleClaims];
                DROP TABLE IF EXISTS [AspNetUserClaims];
                DROP TABLE IF EXISTS [AspNetUserLogins];
                DROP TABLE IF EXISTS [AspNetUserRoles];
                DROP TABLE IF EXISTS [AspNetUserTokens];
                DROP TABLE IF EXISTS [AspNetRoles];
                DROP TABLE IF EXISTS [AspNetUsers];
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_AppUsers_UserId",
                table: "UserProfiles",
                column: "UserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_UserProfiles_AppUsers_UserId')
                    ALTER TABLE [UserProfiles] DROP CONSTRAINT [FK_UserProfiles_AppUsers_UserId];
                """);

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
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
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
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
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail",
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_AspNetUsers_UserId",
                table: "UserProfiles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FlashcardSets_AspNetUsers_UserId",
                table: "FlashcardSets",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StudySessions_AspNetUsers_UserId",
                table: "StudySessions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserProgresses_AspNetUsers_UserId",
                table: "UserProgresses",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserStudySettings_AspNetUsers_UserId",
                table: "UserStudySettings",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
