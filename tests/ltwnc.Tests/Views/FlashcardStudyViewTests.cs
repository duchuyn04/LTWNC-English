namespace ltwnc.Tests.Views;

public sealed class FlashcardStudyViewTests
{
    [Fact]
    public void FlashcardStudyCompletesWithoutRenderingCompletionFeedback()
    {
        string view = Read("Views/Study/Flashcard.cshtml");

        Assert.DoesNotContain("completion-view", view);
        Assert.DoesNotContain("sessionStats", view);
        Assert.DoesNotContain("showCompletionScreen", view);
        Assert.Contains("const ratedCards = new Set();", view);
        Assert.Contains("saveProgress.finally(completeStudySession);", view);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(startPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ltwnc.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
