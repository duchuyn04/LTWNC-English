namespace ltwnc.Tests.Views;

public class QuizResumeContrastTests
{
    private static string Root => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Css => File.ReadAllText(
        Path.Combine(Root, "wwwroot/css/quiz.css"));

    [Fact]
    public void ResumeCard_UsesLightenedSurfaceAndReadableText()
    {
        Assert.Contains(".quiz-active-session", Css);
        Assert.Contains("background: #3a3734", Css);
        Assert.Contains("border: 1px solid rgba(255, 255, 255, 0.12)", Css);
        Assert.Contains(".quiz-active-session h2", Css);
        Assert.Contains("color: #fffaf5", Css);
        Assert.Contains("color: #e7e1db", Css);
    }
}
