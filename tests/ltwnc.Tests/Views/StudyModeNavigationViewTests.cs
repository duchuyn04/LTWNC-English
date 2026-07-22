namespace ltwnc.Tests.Views;

public sealed class StudyModeNavigationViewTests
{
    private static readonly string FlashcardView =
        ReadFile("Views", "Study", "Flashcard.cshtml");

    private static readonly string FlashcardStyles =
        ReadFile("wwwroot", "css", "flashcard.css");

    [Fact]
    public void Flashcard_navigation_renders_every_registered_mode_from_the_model()
    {
        Assert.Contains("foreach (var mode in Model.Modes)", FlashcardView);
        Assert.Contains("mode.Mode == StudyMode.Flashcard", FlashcardView);
        Assert.Contains("href=\"@mode.ActionUrl\"", FlashcardView);
        Assert.Contains("@mode.IconClass", FlashcardView);
        Assert.Contains("@mode.Name", FlashcardView);
        Assert.Contains("mode.UnavailableReason", FlashcardView);
    }

    [Fact]
    public void Flashcard_navigation_does_not_hard_code_a_subset_of_modes()
    {
        Assert.DoesNotContain("var dictationMode =", FlashcardView);
        Assert.DoesNotContain("var missionMode =", FlashcardView);
    }

    [Fact]
    public void Flashcard_navigation_uses_a_balanced_mobile_grid_for_four_modes()
    {
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", FlashcardStyles);
        Assert.Contains("flex-wrap: wrap;", FlashcardStyles);
        Assert.Matches(
            "(?s)@media \\(max-width: 767px\\).*?\\.study-mode-tabs\\s*\\{.*?grid-template-columns: repeat\\(2, minmax\\(0, 1fr\\)\\);",
            FlashcardStyles);
    }

    private static string ReadFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        return string.Empty;
    }
}
