namespace ltwnc.Tests.Views;

public sealed class LeaderboardMarkupTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void ProductionLeaderboard_UsesRealModelAndPeriodLinks()
    {
        string view = File.ReadAllText(Path.Combine(Root, "Views", "Leaderboard", "Index.cshtml"));

        Assert.Contains("LeaderboardPageViewModel", view);
        Assert.Contains("PeriodLink(7)", view);
        Assert.Contains("PeriodLink(30)", view);
        Assert.Contains("Model.Entries", view);
        Assert.DoesNotContain("mock", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("A. Podium", view, StringComparison.OrdinalIgnoreCase);
    }
}
