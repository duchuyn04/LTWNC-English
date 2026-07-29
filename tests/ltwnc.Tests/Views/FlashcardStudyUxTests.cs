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
    public void StudyWorkspace_UsesResponsiveSplitStudioLayout()
    {
        string view = Read("Views/Study/Flashcard.cshtml");
        string css = Read("wwwroot/css/flashcard.css");

        Assert.Contains("class=\"study-focus-column\"", view);
        Assert.Contains("class=\"study-vocabulary-section\"", view);
        Assert.Contains("grid-template-areas:", css);
        Assert.Contains("\"focus vocabulary\"", css);
        Assert.Contains("@media (min-width: 64rem)", css);
        Assert.Contains("overflow-x: clip", css);
        Assert.Contains("prefers-reduced-motion: reduce", css);
        Assert.DoesNotContain("scrollToStudyAreaOnEntry", view);
        Assert.Contains("class=\"vocab-star", view);
        Assert.Contains("aria-pressed=", view);
        Assert.Contains("function toggleVocabularyCardStar(card)", view);
        Assert.Contains("\"index term audio\"", css);
        Assert.Contains("\". meaning audio\"", css);
        Assert.Contains("grid-area: audio", css);
    }

    [Fact]
    public void FlashcardWorkspace_UsesOneFontAndARefinedCardMeasure()
    {
        string css = Read("wwwroot/css/flashcard.css");

        Assert.DoesNotContain("font-family: var(--font-display)", css);
        Assert.Contains("width: min(100%, 46rem)", css);
        Assert.Contains("max-height: 40rem", css);
        Assert.Contains("transition: transform 560ms var(--ease-out)", css);
        Assert.Contains("font-size: clamp(3.25rem, 5.5vw, 5.5rem)", css);
        Assert.Contains("font-size: clamp(2.75rem, 4.5vw, 4.5rem)", css);
        Assert.Contains(".study-vocabulary-header h2 {\n    margin: 0;\n    white-space: nowrap", css);
    }

    [Fact]
    public void StudyNavigation_CommunicatesBoundaryAndCompletionStates()
    {
        string view = Read("Views/Study/Flashcard.cshtml");

        Assert.Contains("function updateNavigationState()", view);
        Assert.Contains("previousButton.disabled = currentIndex === 0", view);
        Assert.Contains("currentIndex = (currentIndex + 1) % flashcards.length", view);
        Assert.Contains("const ratedCount = sessionStats.learned.size + sessionStats.unlearned.size", view);
        Assert.Contains("if (ratedCount === flashcards.length)", view);
        Assert.DoesNotContain("if (currentIndex < flashcards.length - 1)", view);
        Assert.Contains("Quay lại thẻ đầu", view);
        Assert.Contains("setAttribute('aria-disabled'", view);
    }
}
