namespace ltwnc.Tests.Views;

public sealed class DictationViewTests
{
    private static string Root => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public void DictationRetryKeepsUnknownActionAndRevealsFeedback()
    {
        string view = File.ReadAllText(Path.Combine(Root, "Views", "Study", "Dictation.cshtml"));
        string styles = File.ReadAllText(Path.Combine(Root, "wwwroot", "css", "dictation-redesign.css"));

        Assert.Contains("dontKnowBtn.hidden = false", view);
        Assert.Contains("checkRetryAnswer(true)", view);
        Assert.Contains("feedbackPanel.scrollIntoView", view);
        Assert.Contains("role=\"status\"", view);
        Assert.Contains("scroll-margin-block", styles);
    }
}
