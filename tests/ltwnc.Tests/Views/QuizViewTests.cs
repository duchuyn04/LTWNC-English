namespace ltwnc.Tests.Views;

public class QuizViewTests
{
    private static readonly string QuizView = ReadFile("Views", "Study", "Quiz.cshtml");
    private static readonly string ResultView = ReadFile("Views", "Study", "QuizResult.cshtml");
    private static readonly string QuizScript = ReadFile("wwwroot", "js", "quiz.js");
    private static readonly string QuizStyles = ReadFile("wwwroot", "css", "quiz.css");

    [Fact]
    public void Quiz_view_renders_server_choices_and_submission_contract_without_answer_leakage()
    {
        Assert.Contains("data-quiz-answer", QuizView);
        Assert.Contains("data-quiz-root", QuizView);
        Assert.Contains("data-question-id=\"@Model.QuestionId\"", QuizView);
        Assert.Contains("data-answer-url", QuizView);
        Assert.Contains("for (int index = 0; index < Model.Choices.Count; index++)", QuizView);
        Assert.Contains("data-choice-index=\"@index\"", QuizView);
        Assert.Contains("@Html.AntiForgeryToken()", QuizView);
        Assert.Contains("aria-live=\"polite\"", QuizView);
        Assert.DoesNotContain("CorrectChoiceIndex", QuizView);
    }

    [Fact]
    public void Quiz_view_loads_versioned_assets_and_keeps_next_navigation_hidden()
    {
        Assert.Contains("~/css/quiz.css", QuizView);
        Assert.Contains("~/js/quiz.js", QuizView);
        Assert.Contains("asp-append-version=\"true\"", QuizView);
        Assert.Contains("data-quiz-next", QuizView);
        Assert.Contains("hidden", QuizView);
    }

    [Fact]
    public void Quiz_script_posts_antiforgery_and_uses_only_the_server_grade()
    {
        Assert.Contains("RequestVerificationToken", QuizScript);
        Assert.Contains("selectedChoiceIndex", QuizScript);
        Assert.Contains("button.disabled = true", QuizScript);
        Assert.Contains("correctChoiceIndex", QuizScript);
        Assert.Contains("result.nextUrl", QuizScript);
        Assert.Contains("result.isLastQuestion", QuizScript);
        Assert.Contains("textContent", QuizScript);
        Assert.DoesNotContain("innerHTML", QuizScript);
        Assert.DoesNotContain("dataset.correct", QuizScript);
    }

    [Fact]
    public void Quiz_script_retries_transient_failures_and_reloads_conflicts()
    {
        Assert.Contains("response.status === 409", QuizScript);
        Assert.Contains("window.location.reload()", QuizScript);
        Assert.Contains("button.disabled = false", QuizScript);
        Assert.Contains("response.status >= 500", QuizScript);
    }

    [Fact]
    public void Result_view_encodes_snapshots_and_offers_expected_actions()
    {
        Assert.Contains("@answer.PromptText", ResultView);
        Assert.Contains("@answer.SelectedAnswer", ResultView);
        Assert.Contains("@answer.CorrectAnswer", ResultView);
        Assert.Contains("Model.WrongAnswers.Any()", ResultView);
        Assert.Contains("RetryWrong", ResultView);
        Assert.Contains("RetryAll", ResultView);
        Assert.Contains("@Html.AntiForgeryToken()", ResultView);
        Assert.Contains("asp-action=\"Index\"", ResultView);
    }

    [Fact]
    public void Quiz_styles_preserve_feedback_focus_and_motion_accessibility()
    {
        Assert.Contains(".quiz-choice.is-correct", QuizStyles);
        Assert.Contains(".quiz-choice.is-wrong", QuizStyles);
        Assert.Contains(":focus-visible", QuizStyles);
        Assert.Contains(".quiz-choice:disabled.is-correct", QuizStyles);
        Assert.Contains(".quiz-choice:disabled.is-wrong", QuizStyles);
        Assert.Contains("@media (max-width:", QuizStyles);
        Assert.Contains("prefers-reduced-motion: reduce", QuizStyles);
    }

    private static string ReadFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        return string.Empty;
    }
}
