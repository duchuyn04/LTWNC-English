namespace ltwnc.Tests.Views;

// Kiểm tra markup nguồn của trang /Library production và điều hướng công khai.
public class PublicLibraryMarkupTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    [Fact]
    public void LibraryView_UsesGetFormAndRealDataWithoutPrototypeArtifacts()
    {
        string view = Read("Views/Library/Index.cshtml");

        Assert.Contains("method=\"get\"", view);
        Assert.Contains("name=\"q\"", view);
        Assert.Contains("name=\"sort\"", view);
        Assert.Contains("Model.Summary.SetCount", view);
        Assert.Contains("Model.Summary.CardCount", view);
        Assert.Contains("Model.Summary.CopyCount", view);
        Assert.Contains("asp-route-page", view);
        Assert.Contains("/Set/@item.Id", view);
        Assert.DoesNotContain("PROTOTYPE", view);
        Assert.DoesNotContain("data-variant", view);
        Assert.DoesNotContain("data-favorite", view);
        Assert.DoesNotContain("IELTS Academic 7.0+", view);
    }

    [Fact]
    public void Layout_ExposesPublicLibraryLinkAndKeepsPersonalLibraryForSignedInUsers()
    {
        string layout = Read("Views/Shared/_Layout.cshtml");

        Assert.Contains("href=\"/Library\"", layout);
        Assert.Contains("/Set", layout);
    }

    [Fact]
    public void HomeExplorationCta_PointsToPublicLibrary()
    {
        string home = Read("Views/Home/Index.cshtml");

        Assert.Contains("href=\"/Library\"", home);
        Assert.DoesNotContain("href=\"#featured-sets\"", home);
    }
}
