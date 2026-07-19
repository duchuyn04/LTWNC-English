using System.Text.RegularExpressions;

namespace ltwnc.Tests.Views;

public class QuizViewTests
{
    private static readonly string QuizView = ReadFile("Views", "Study", "Quiz.cshtml");
    private static readonly string QuizSetupView = ReadFile("Views", "Study", "QuizSetup.cshtml");
    private static readonly string ResultView = ReadFile("Views", "Study", "QuizResult.cshtml");
    private static readonly string ResultActionsPartial = ReadFile("Views", "Study", "_QuizResultActions.cshtml");
    private static readonly string QuizScript = ReadFile("wwwroot", "js", "quiz.js");
    private static readonly string QuizStyles = ReadFile("wwwroot", "css", "quiz.css");

    [Fact]
    public void Quiz_setup_view_offers_presets_custom_duration_and_active_continuation()
    {
        Assert.Contains("~/css/quiz.css", QuizSetupView);
        Assert.Contains("value=\"5\"", QuizSetupView);
        Assert.Contains("value=\"10\"", QuizSetupView);
        Assert.Contains("value=\"15\"", QuizSetupView);
        Assert.Contains("value=\"20\"", QuizSetupView);
        Assert.Contains("type=\"number\"", QuizSetupView);
        Assert.Contains("min=\"1\"", QuizSetupView);
        Assert.Contains("max=\"120\"", QuizSetupView);
        Assert.Contains("@Html.AntiForgeryToken()", QuizSetupView);
        Assert.Contains("asp-route=\"QuizStartPost\"", QuizSetupView);
        Assert.Contains("asp-controller=\"Study\"", QuizSetupView);
        Assert.Contains("Model.ActiveSessionId.HasValue", QuizSetupView);
        Assert.Contains("asp-action=\"Quiz\"", QuizSetupView);
        Assert.DoesNotContain("Ã", QuizSetupView);
    }

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
        Assert.Contains("if (Model.IsReviewOnly", QuizView);
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
    public void Quiz_view_exposes_deadline_timeout_and_timer_contracts()
    {
        Assert.Contains("data-quiz-deadline-utc", QuizView);
        Assert.Contains("data-quiz-timeout-url", QuizView);
        Assert.Contains("Model.DeadlineUtc", QuizView);
        Assert.Contains("QuizTimeout", QuizView);
        Assert.Contains("data-quiz-timer", QuizView);
        Assert.Contains("@Html.AntiForgeryToken()", QuizView);
        Assert.Contains("data-quiz-remaining-seconds=\"@Model.RemainingSeconds\"", QuizView);
    }

    [Fact]
    public void Quiz_header_groups_restart_with_exit_actions_to_preserve_grid_layout()
    {
        string header = RequiredMatch(
            QuizView,
            "<header class=\"quiz-header\">[\\s\\S]*?</header>");
        string actions = RequiredMatch(
            header,
            "<div class=\"quiz-header-actions\">[\\s\\S]*?</div>");

        Assert.Contains("class=\"quiz-exit\"", actions);
        Assert.Contains("asp-action=\"QuizRestart\"", actions);
        Assert.Contains("@Html.AntiForgeryToken()", actions);
        Assert.Contains("onsubmit=\"return confirm", actions);
        Assert.DoesNotContain("<form", Regex.Replace(header, "<div class=\"quiz-header-actions\">[\\s\\S]*?</div>", string.Empty));
        Assert.Contains(".quiz-header-actions", QuizStyles);
    }

    [Fact]
    public void Quiz_view_renders_read_only_review_choices_and_question_navigation()
    {
        Assert.Contains("data-quiz-review-only", QuizView);
        Assert.Contains("Model.IsReviewOnly", QuizView);
        Assert.Contains("Model.SelectedChoiceIndex", QuizView);
        Assert.Contains("Model.CorrectChoiceIndex", QuizView);
        Assert.Contains("is-correct", QuizView);
        Assert.Contains("is-wrong", QuizView);
        Assert.Contains("disabled", QuizView);
        Assert.Contains("Model.PreviousQuestionId", QuizView);
        Assert.Contains("Model.NextQuestionId", QuizView);
        Assert.Contains("Model.IsReviewOnly && Model.NextQuestionId.HasValue", QuizView);
        Assert.Contains("Model.CurrentPendingQuestionId", QuizView);
        Assert.Contains("asp-route-questionId", QuizView);
        Assert.Contains("Quay lại câu đang làm", QuizView);
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
    public void Quiz_script_tracks_the_server_deadline_and_completes_timeout_once()
    {
        Assert.Contains("Date.parse(root.dataset.quizDeadlineUtc)", QuizScript);
        Assert.Contains("deadlineUtc - Date.now()", QuizScript);
        Assert.Contains("window.setInterval(updateTimer", QuizScript);
        Assert.Contains("timer.classList.toggle('is-warning'", QuizScript);
        Assert.Contains("let timeoutRequested = false", QuizScript);
        Assert.Contains("if (timeoutRequested) return;", QuizScript);
        Assert.Contains("root.dataset.quizTimeoutUrl", QuizScript);
        Assert.Contains("'RequestVerificationToken': token", QuizScript);
        Assert.Contains("window.location.assign(result.nextUrl)", QuizScript);
        Assert.Contains("serverRemainingSeconds", QuizScript);
        Assert.Contains("calibratedDeadlineUtc", QuizScript);
        Assert.Contains("result.expired === false", QuizScript);
        Assert.Contains("timeoutRequested = false", QuizScript);
        Assert.Contains("setPending(false)", QuizScript);
    }

    [Fact]
    public void Quiz_script_redirects_expired_answer_responses_to_the_result()
    {
        string conflictBranch = RequiredMatch(
            QuizScript,
            "if \\(response\\.status === 409\\) \\{[\\s\\S]*?return;[\\s\\S]*?\\n                \\}");

        Assert.Contains("const result = await response.json()", conflictBranch);
        Assert.Contains("result.expired && result.nextUrl", conflictBranch);
        Assert.Contains("window.location.assign(result.nextUrl)", conflictBranch);
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
    public void Result_actions_are_rendered_twice_from_a_shared_partial()
    {
        Assert.Contains("@answer.PromptText", ResultView);
        Assert.Contains("@answer.SelectedAnswer", ResultView);
        Assert.Contains("@answer.CorrectAnswer", ResultView);
        Assert.Equal(2, Regex.Matches(
            ResultView,
            "<partial(?=[^>]*name=\"_QuizResultActions\")(?=[^>]*model=\"Model\")[^>]*/>",
            RegexOptions.Singleline).Count);
        Assert.DoesNotContain("asp-action=\"RetryWrong\"", ResultView);
        Assert.DoesNotContain("asp-action=\"RetryAll\"", ResultView);
        Assert.DoesNotContain("@Html.AntiForgeryToken()", ResultView);
        Assert.DoesNotContain("asp-action=\"Index\"", ResultView);

        string retryWrongConditional = RequiredMatch(
            ResultActionsPartial,
            "@if \\(Model\\.WrongAnswers\\.Any\\(\\)\\)\\s*\\{\\s*" +
            "<form(?=[^>]*asp-action=\"RetryWrong\")[\\s\\S]*?</form>\\s*\\}");

        Assert.Contains("Model.WrongAnswers.Any()", retryWrongConditional);
        Assert.Contains("@Html.AntiForgeryToken()", retryWrongConditional);
        Assert.Contains("asp-action=\"RetryAll\"", ResultActionsPartial);
        Assert.Equal(2, Regex.Matches(ResultActionsPartial, "@Html.AntiForgeryToken\\(\\)").Count);
        Assert.Contains("asp-action=\"Index\"", ResultActionsPartial);
    }

    [Fact]
    public void Result_view_consumes_retry_feedback_as_razor_encoded_accessible_status()
    {
        string feedbackConditional = RequiredMatch(
            ResultView,
            "@if \\(TempData\\[\\\"Message\\\"\\] is string message[\\s\\S]*?" +
            "<div[^>]*role=\\\"status\\\"[^>]*aria-live=\\\"polite\\\"[^>]*>[\\s\\S]*?</div>");

        Assert.Contains("@message", feedbackConditional);
        Assert.DoesNotContain("Html.Raw", feedbackConditional);
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

    [Fact]
    public void Quiz_styles_cover_timer_and_attempt_controls_responsively()
    {
        string header = RequiredMatch(QuizStyles, "\\.quiz-header \\{[\\s\\S]*?\\}");

        Assert.Contains("grid-template-columns: auto minmax(0, 1fr) auto auto;", header);
        Assert.Contains(".quiz-timer", QuizStyles);
        Assert.Contains(".quiz-timer.is-warning", QuizStyles);
        Assert.Contains(".quiz-restart", QuizStyles);
        Assert.Contains(".quiz-previous", QuizStyles);
        Assert.Contains(".quiz-next-question", QuizStyles);
        Assert.Contains(".quiz-return-current", QuizStyles);
        Assert.Contains(".quiz-previous:focus-visible", QuizStyles);
        Assert.Contains("grid-column: 1 / -1;", QuizStyles);
        Assert.Contains(".quiz-timer", RequiredMatch(QuizStyles, "@media \\(prefers-reduced-motion: reduce\\) \\{[\\s\\S]*?\\n\\}"));
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
