namespace ltwnc.Tests.Views;

public sealed class HomeCreditPackagesViewTests
{
    [Fact]
    public void HomeIndex_RendersDynamicCreditPackagesWithAccessiblePurchaseLinks()
    {
        string root = FindRepositoryRoot();
        string view = File.ReadAllText(Path.Combine(root, "Views", "Home", "Index.cshtml"));

        Assert.Contains("Model.CreditPackages.Any()", view);
        Assert.Contains("home-credit-board", view);
        Assert.Contains("home-credit-options", view);
        Assert.Contains("1 phản hồi", view);
        Assert.Contains("@package.Credits", view);
        Assert.Contains("@package.PriceVnd", view);
        Assert.Contains("aria-label=\"Chọn gói @package.Name\"", view);
        Assert.Contains("VietQR · một lần", view);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ltwnc.csproj")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc repository.");
    }
}
