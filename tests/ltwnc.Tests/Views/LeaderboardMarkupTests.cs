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

    [Fact]
    public void ProductionLeaderboard_UsesModerateHeadingScale()
    {
        string css = File.ReadAllText(Path.Combine(Root, "wwwroot", "css", "leaderboard.css"));

        Assert.Contains("font-size:clamp(2rem,4vw,3rem)", css);
        Assert.Contains("font-size:clamp(1.625rem,3vw,2.25rem)", css);
        Assert.DoesNotContain("font-size:clamp(2rem,5vw,4.25rem)", css);
        Assert.DoesNotContain("font-size:clamp(2rem,5vw,3.6rem)", css);
    }
}
