namespace ltwnc.Tests.Migrations;

public sealed class DropIdentityTablesMigrationTests
{
    private static readonly string Migration = ReadMigration();

    [Fact]
    public void Up_preserves_existing_identity_accounts_before_dropping_identity_tables()
    {
        Assert.Contains("INSERT INTO [AppUsers]", Migration);
        Assert.Contains("FROM [AspNetUsers]", Migration);
        Assert.DoesNotContain("DELETE FROM [UserProfiles]", Migration);
    }

    [Fact]
    public void Up_tolerates_foreign_keys_that_were_removed_by_older_migrations()
    {
        Assert.Contains("IF EXISTS", Migration);
        Assert.Contains("sys.foreign_keys", Migration);
        Assert.DoesNotContain("migrationBuilder.DropForeignKey", Migration);
    }

    private static string ReadMigration()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "Migrations",
                "20260721084654_DropIdentityTables.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        return string.Empty;
    }
}
