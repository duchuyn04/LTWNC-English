namespace ltwnc.Tests.Views;

public class NotFoundMarkupTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void NotFoundView_HasAccessibilityAndLocalAssets()
    {
        string view = File.ReadAllText(Path.Combine(Root, "Views", "Shared", "NotFound.cshtml"));
        string css = File.ReadAllText(Path.Combine(Root, "wwwroot", "css", "not-found.css"));

        Assert.Contains("HideLayoutChrome", view);
        Assert.Contains("FullWidth", view);
        Assert.Contains("Về trang chủ", view);
        Assert.Contains("Lật thẻ", view);
        Assert.Contains("aria-pressed", view);
        Assert.Contains("not-found.css", view);
        Assert.Contains("not-found.js", view);
        Assert.DoesNotContain("-->", view);
        Assert.DoesNotContain("<--", view);
        Assert.Contains("prefers-reduced-motion", css);
    }
}
