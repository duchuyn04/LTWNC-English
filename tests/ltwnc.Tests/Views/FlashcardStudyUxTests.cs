namespace ltwnc.Tests.Views;

public class FlashcardStudyUxTests
{
    private static string Root => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath));

    [Fact]
    public void PrimaryStudyControls_RemainAvailableInAViewportDock()
    {
        string view = Read("Views/Study/Flashcard.cshtml");
        string css = Read("wwwroot/css/flashcard.css");

        Assert.Contains("id=\"dock-progress-text\"", view);
        Assert.Contains("id=\"nav-left-btn\"", view);
        Assert.Contains("id=\"flip-btn\"", view);
        Assert.Contains("id=\"rating-group\"", view);
        Assert.Contains("id=\"nav-right-btn\"", view);
        Assert.Contains("position: fixed", css);
        Assert.Contains("env(safe-area-inset-bottom)", css);
    }

    [Fact]
    public void StudyNavigation_CommunicatesBoundaryAndCompletionStates()
    {
        string view = Read("Views/Study/Flashcard.cshtml");

        Assert.Contains("function updateNavigationState()", view);
        Assert.Contains("previousButton.disabled = currentIndex === 0", view);
        Assert.Contains("Hoàn thành phiên học", view);
        Assert.Contains("setAttribute('aria-disabled'", view);
    }
}
