namespace ltwnc.Tests.Views;

public class AuthLayoutMarkupTests
{
    private static string Root => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath));

    [Fact]
    public void AuthLayout_UsesDedicatedShellAndLocalResponsiveImage()
    {
        string layout = Read("Views/Shared/_AuthLayout.cshtml");

        Assert.Contains("@RenderBody()", layout);
        Assert.Contains("auth-shell", layout);
        Assert.Contains("auth-panel", layout);
        Assert.Contains("auth-studio", layout);
        Assert.Contains("<picture", layout);
        Assert.Contains("auth-learning-studio.webp", layout);
        Assert.Contains("auth-learning-studio-mobile.webp", layout);
        Assert.Contains("auth-learning-studio.jpg", layout);
        Assert.Contains("~/css/auth.css", layout);
        Assert.Contains("~/js/auth.js", layout);
        Assert.DoesNotContain("images.unsplash.com", layout);
        Assert.DoesNotContain("app-nav", layout);
        Assert.DoesNotContain("app-footer", layout);
    }

    [Fact]
    public void AuthLayout_KeepsFormFirstInDomWhileCssPlacesStudioLeft()
    {
        string layout = Read("Views/Shared/_AuthLayout.cshtml");
        string css = Read("wwwroot/css/auth.css");

        int panelIndex = layout.IndexOf("auth-panel", StringComparison.Ordinal);
        int studioIndex = layout.IndexOf("auth-studio", StringComparison.Ordinal);

        Assert.True(panelIndex >= 0 && studioIndex > panelIndex);
        Assert.Contains("grid-template-areas: \"studio panel\"", css);
        Assert.Contains("grid-template-columns: minmax(0, 58fr) minmax(420px, 42fr)", css);
    }

    [Fact]
    public void AuthStyles_EncodeViewportAndResponsiveContracts()
    {
        string css = Read("wwwroot/css/auth.css");

        Assert.Contains("min-height: 100vh", css);
        Assert.Contains("min-height: 100dvh", css);
        Assert.Contains("@media (max-width: 980px)", css);
        Assert.Contains("grid-template-areas: \"panel\" \"studio\"", css);
        Assert.Contains("height: 280px", css);
        Assert.Contains("@media (max-width: 640px)", css);
        Assert.Contains("height: 220px", css);
        Assert.Contains("@media (max-height: 780px) and (min-width: 981px)", css);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
        Assert.Contains("overflow-y: auto", css);
    }

    [Fact]
    public void AuthImageAssets_AreLocalAndWithinBudgets()
    {
        string desktopWebp = Path.Combine(Root, "wwwroot", "images", "auth", "auth-learning-studio.webp");
        string mobileWebp = Path.Combine(Root, "wwwroot", "images", "auth", "auth-learning-studio-mobile.webp");
        string jpegFallback = Path.Combine(Root, "wwwroot", "images", "auth", "auth-learning-studio.jpg");

        Assert.True(File.Exists(desktopWebp));
        Assert.True(File.Exists(mobileWebp));
        Assert.True(File.Exists(jpegFallback));
        Assert.InRange(new FileInfo(desktopWebp).Length, 1, 300 * 1024);
        Assert.InRange(new FileInfo(mobileWebp).Length, 1, 140 * 1024);
    }
}
