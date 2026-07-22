namespace ltwnc.Tests.Views;

public class MotionPolicyTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath));

    [Fact]
    public void Layout_DoesNotLoadDecorativeAnimationRuntimeOrPageOverlay()
    {
        string layout = Read("Views/Shared/_Layout.cshtml");
        string siteScript = Read("wwwroot/js/site.js");

        Assert.DoesNotContain("gsap", layout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ScrollTrigger", layout);
        Assert.DoesNotContain("pt-overlay", layout);
        Assert.DoesNotContain("page-transition.js", layout);
        Assert.DoesNotContain("gsap", siteScript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Views_DoNotKeepGsapUtilityHooks()
    {
        string[] views = Directory.GetFiles(Path.Combine(Root, "Views"), "*.cshtml", SearchOption.AllDirectories);

        foreach (string view in views)
        {
            Assert.DoesNotContain("gsap-", File.ReadAllText(view), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void HomeCarousel_IsManualAndHomeContentHasNoRevealHooks()
    {
        string view = Read("Views/Home/Index.cshtml");
        string script = Read("wwwroot/js/home.js");

        Assert.Contains("data-carousel-previous", view);
        Assert.Contains("data-carousel-next", view);
        Assert.Contains("data-carousel-card", view);
        Assert.DoesNotContain("data-autoplay-ms", view);
        Assert.DoesNotContain("data-carousel-toggle", view);
        Assert.DoesNotContain("data-home-reveal", view);
        Assert.DoesNotContain("setInterval", script);
        Assert.DoesNotContain("IntersectionObserver", script);
    }

    [Fact]
    public void StudyResults_RenderFinalStateWithoutDecorativeAnimation()
    {
        string flashcard = Read("Views/Study/Flashcard.cshtml");
        string dictationResult = Read("Views/Study/DictationResult.cshtml");

        Assert.DoesNotContain("fireworks", flashcard, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestAnimationFrame", flashcard);
        Assert.DoesNotContain("is-exit-left", flashcard);
        Assert.DoesNotContain("requestAnimationFrame", dictationResult);
        Assert.DoesNotContain("js-count", dictationResult);
        Assert.Contains("stroke-dasharray=\"@Model.Score 100\"", dictationResult);
        Assert.Contains("style=\"width:@correctPct%\"", dictationResult);
    }

    [Fact]
    public void SharedStyles_UseShortMotionTokensAndDisableMotionForReducedPreference()
    {
        string styles = Read("wwwroot/css/site.css");

        Assert.Contains("--duration-fast: 120ms", styles);
        Assert.Contains("--duration-med: 180ms", styles);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles);
        Assert.Contains("animation-duration: 0.01ms !important", styles);
        Assert.Contains("transition-duration: 0.01ms !important", styles);
        Assert.Contains("scroll-behavior: auto !important", styles);
    }
}
