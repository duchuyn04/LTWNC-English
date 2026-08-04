namespace ltwnc.Tests.Views;

public sealed class CreditsViewTests
{
    [Fact]
    public void CreditsIndex_RendersUsageDashboardAndPackageOffcanvas()
    {
        string root = FindRepositoryRoot();
        string view = File.ReadAllText(Path.Combine(root, "Views", "Credits", "Index.cshtml"));

        Assert.Contains("credit-hero", view);
        Assert.Contains("data-bs-toggle=\"offcanvas\"", view);
        Assert.Contains("creditPackagePanel", view);
        Assert.Contains("Model.Usage.CreditsUsedThisMonth", view);
        Assert.Contains("Model.Usage.Breakdown", view);
        Assert.Contains("role=\"progressbar\"", view);
        Assert.Contains("creditLedgerDetails", view);
        Assert.Contains("asp-action=\"Buy\"", view);
        Assert.Contains("CreditPurchaseStatuses.Paid", view);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ltwnc.csproj")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
