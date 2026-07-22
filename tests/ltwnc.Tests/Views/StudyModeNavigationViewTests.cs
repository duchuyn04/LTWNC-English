namespace ltwnc.Tests.Views;

public sealed class StudyModeNavigationViewTests
{
    private static readonly string FlashcardView =
        ReadFile("Views", "Study", "Flashcard.cshtml");

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
