using System.Text.RegularExpressions;

namespace ltwnc.Tests.Data;

public sealed class QuizTimingMigrationContractTests
{
    [Fact]
    public void Downgrade_reconciles_only_quiz_score_null_rows_before_legacy_unique_index()
    {
        string migration = ReadMigration();
        string down = migration[migration.IndexOf("protected override void Down", StringComparison.Ordinal)..];

        int sqlIndex = down.IndexOf("migrationBuilder.Sql", StringComparison.Ordinal);
        int indexCreation = down.IndexOf("migrationBuilder.CreateIndex", StringComparison.Ordinal);
        Assert.True(sqlIndex >= 0, "Downgrade must reconcile data before recreating the legacy index.");
        Assert.True(indexCreation > sqlIndex);

        string sql = down[sqlIndex..indexCreation];
        Assert.Contains("StudySessions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Mode] = 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Score] IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROW_NUMBER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "PARTITION BY [UserId], [FlashcardSetId], [Mode]",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Matches(
            new Regex("UPDATE\\s+session[\\s\\S]*FROM\\s+\\[StudySessions\\]", RegexOptions.IgnoreCase),
            sql);
    }

    private static string ReadMigration()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "Migrations",
                "20260719154400_AddQuizTimingAndActiveSessionState.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        return string.Empty;
    }
}
