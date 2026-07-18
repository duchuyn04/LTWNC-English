namespace ltwnc.Tests.Views;

public class NotFoundPrototypeTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Prototype_ContainsWrongTurnContentAndHomeLink()
    {
        string html = File.ReadAllText(Path.Combine(Root, "prototype", "404", "index.html"));

        Assert.Contains("Bạn vừa rẽ nhầm một hướng.", html);
        Assert.Contains("wrong turn", html);
        Assert.Contains("href=\"/\"", html);
        Assert.Contains("Về trang chủ", html);
        Assert.DoesNotContain("-->", html);
        Assert.DoesNotContain("<--", html);
    }

    [Fact]
    public void Prototype_ReferencesLocalCssAndJavascript()
    {
        string html = File.ReadAllText(Path.Combine(Root, "prototype", "404", "index.html"));

        Assert.Contains("href=\"404.css\"", html);
        Assert.Contains("src=\"404.js\"", html);
    }

    [Fact]
    public void Prototype_SupportsKeyboardStateAndReducedMotion()
    {
        string script = File.ReadAllText(Path.Combine(Root, "prototype", "404", "404.js"));
        string css = File.ReadAllText(Path.Combine(Root, "prototype", "404", "404.css"));

        Assert.Contains("aria-pressed", script);
        Assert.Contains("Space", script);
        Assert.Contains("prefers-reduced-motion", css);
        Assert.Contains("@media", css);
    }
}
