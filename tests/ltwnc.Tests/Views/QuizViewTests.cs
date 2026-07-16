using System.Text.RegularExpressions;

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
        string clickHandler = RequiredMatch(
            QuizScript,
            "button\\.addEventListener\\('click', async \\(\\) => \\{[\\s\\S]*?\\n        \\}\\);");
        string serverGrade = RequiredMatch(
            clickHandler,
            "const result = await response\\.json\\(\\);[\\s\\S]*?nextLink\\.hidden = false;");

        Assert.Contains("RequestVerificationToken", clickHandler);
        Assert.Contains("selectedChoiceIndex", clickHandler);
        Assert.Matches(
            new Regex("setPending\\(true\\);[\\s\\S]*?await fetch", RegexOptions.Singleline),
            clickHandler);
        Assert.Contains("const correctChoiceIndex = Number(result.correctChoiceIndex)", serverGrade);
        Assert.Contains("const correctButton = buttons[correctChoiceIndex]", serverGrade);
        Assert.Contains("correctButton.classList.add('is-correct')", serverGrade);
        Assert.Contains("result.isCorrect === false", serverGrade);
        Assert.Contains("result.nextUrl", serverGrade);
        Assert.Contains("result.isLastQuestion", serverGrade);
        Assert.Contains("textContent", serverGrade);
        Assert.DoesNotContain("innerHTML", QuizScript);
        Assert.DoesNotContain("dataset.correct", QuizScript);
    }

    [Fact]
    public void Quiz_script_retries_transient_failures_and_reloads_conflicts()
    {
        string pendingFunction = RequiredMatch(
            QuizScript,
            "const setPending = \\(pending\\) => \\{[\\s\\S]*?\\n    \\};");
        string retryableErrorFunction = RequiredMatch(
            QuizScript,
            "const showRetryableError = \\(\\) => \\{[\\s\\S]*?\\n    \\};");
        string conflictBranch = RequiredMatch(
            QuizScript,
            "if \\(response\\.status === 409\\) \\{[\\s\\S]*?return;[\\s\\S]*?\\n                \\}");
        string requestErrorFlow = RequiredMatch(
            QuizScript,
            "if \\(response\\.status >= 500\\)[\\s\\S]*?catch \\(error\\) \\{[\\s\\S]*?\\n            \\}");

        Assert.Contains("button.disabled = true", pendingFunction);
        Assert.Contains("button.disabled = false", pendingFunction);
        Assert.Contains("setPending(false)", retryableErrorFunction);
        Assert.Contains("window.location.reload()", conflictBranch);
        Assert.Contains("throw new Error('Server error')", requestErrorFlow);
        Assert.Contains("showRetryableError()", requestErrorFlow);
    }

    [Fact]
    public void Quiz_script_announces_and_labels_the_server_selected_correct_choice()
    {
        string serverGrade = RequiredMatch(
            QuizScript,
            "const result = await response\\.json\\(\\);[\\s\\S]*?nextLink\\.hidden = false;");

        Assert.Contains("const correctChoiceText = correctButton.textContent.trim()", serverGrade);
        Assert.Contains(
            "correctButton.setAttribute('aria-label', `Đáp án đúng: ${correctChoiceText}`)",
            serverGrade);
        Assert.Contains(": `Chưa đúng. Đáp án đúng: ${correctChoiceText}.`;", serverGrade);
    }

    [Fact]
    public void Result_view_encodes_snapshots_and_offers_expected_actions()
    {
        string retryWrongConditional = RequiredMatch(
            ResultView,
            "<div class=\"quiz-result-actions\">\\s*" +
            "@if \\(Model\\.WrongAnswers\\.Any\\(\\)\\)\\s*\\{\\s*" +
            "<form(?=[^>]*asp-action=\"RetryWrong\")[\\s\\S]*?</form>\\s*\\}");

        Assert.Contains("@answer.PromptText", ResultView);
        Assert.Contains("@answer.SelectedAnswer", ResultView);
        Assert.Contains("@answer.CorrectAnswer", ResultView);
        Assert.Contains("Model.WrongAnswers.Any()", retryWrongConditional);
        Assert.Contains("asp-action=\"RetryWrong\"", retryWrongConditional);
        Assert.Contains("@Html.AntiForgeryToken()", retryWrongConditional);
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
        Assert.Contains("outline: 3px solid #92400e;", QuizStyles);
        Assert.DoesNotContain("outline: 3px solid rgba(", QuizStyles);
        Assert.Contains("@media (max-width:", QuizStyles);
        Assert.Contains("prefers-reduced-motion: reduce", QuizStyles);
    }

    private static string RequiredMatch(string source, string pattern)
    {
        System.Text.RegularExpressions.Match match = Regex.Match(
            source,
            pattern,
            RegexOptions.Singleline);
        Assert.True(match.Success, $"Required scoped contract did not match: {pattern}");
        return match.Value;
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
