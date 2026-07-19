using System.Reflection;
using System.Text.Json;
using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using ltwnc.Services.Study;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace ltwnc.Tests.Controllers;

public class StudyControllerQuizTests
{
    private readonly Mock<IStudyService> _studyService = new();
    private readonly Mock<IDictationService> _dictationService = new();
    private readonly Mock<IQuizService> _quizService = new();
    private readonly Mock<IFlashcardSetService> _setService = new();

    [Fact]
    public async Task QuizStart_renders_setup_with_active_session_continuation()
    {
        _quizService.Setup(service => service.GetSetupAsync(7, "user-1"))
            .ReturnsAsync(new QuizSetupState
            {
                SetId = 7,
                SetTitle = "Core English",
                ActiveSession = new StudySession { Id = 42 }
            });
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizStart(7);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal("QuizSetup", view.ViewName);
        QuizSetupViewModel model = Assert.IsType<QuizSetupViewModel>(view.Model);
        Assert.Equal(7, model.SetId);
        Assert.Equal("Core English", model.SetTitle);
        Assert.Equal(QuizService.DefaultQuizMinutes, model.SelectedPresetMinutes);
        Assert.Equal(42, model.ActiveSessionId);
        _quizService.Verify(
            service => service.GetSetupAsync(7, "user-1"),
            Times.Once);
    }

    [Fact]
    public async Task QuizStart_post_rejects_invalid_duration()
    {
        _quizService.Setup(service => service.GetSetupAsync(7, "user-1"))
            .ReturnsAsync(new QuizSetupState { SetId = 7, SetTitle = "Core English" });
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizStart(7, new QuizSetupViewModel
        {
            SelectedPresetMinutes = 121
        });

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal("QuizSetup", view.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            $"Thời lượng phải từ 1 đến {QuizService.MaximumQuizMinutes} phút.",
            controller.ModelState[nameof(QuizSetupViewModel.CustomMinutes)]!.Errors
                .Select(error => error.ErrorMessage));
        _quizService.Verify(
            service => service.StartNewAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<UserStudySettings>(),
                It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task QuizStart_post_starts_fresh_attempt_with_selected_duration()
    {
        UserStudySettings settings = new() { StarredOnly = true };
        _studyService.Setup(service => service.GetSettingsAsync("user-1"))
            .ReturnsAsync(settings);
        _quizService.Setup(service => service.StartNewAsync(7, "user-1", settings, 15))
            .ReturnsAsync(new StudySession { Id = 42 });
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizStart(7, new QuizSetupViewModel
        {
            SelectedPresetMinutes = 15
        });

        AssertQuizSessionRedirect(result, setId: 7, sessionId: 42);
    }

    [Fact]
    public async Task Quiz_returns_view_without_correct_answer()
    {
        QuizQuestionState state = CreateQuestionState();
        _quizService.Setup(service => service.GetCurrentQuestionAsync(7, 42, "user-1", null))
            .ReturnsAsync(state);
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.Quiz(7, 42);

        ViewResult view = Assert.IsType<ViewResult>(result);
        QuizStudyViewModel model = Assert.IsType<QuizStudyViewModel>(view.Model);
        Assert.Equal(7, model.SetId);
        Assert.Equal("Core English", model.SetTitle);
        Assert.Equal(42, model.SessionId);
        Assert.Equal(501, model.QuestionId);
        Assert.Equal(3, model.CurrentNumber);
        Assert.Equal(10, model.TotalQuestions);
        Assert.Equal(1, model.CorrectCount);
        Assert.Equal(QuizQuestionDirection.TermToDefinition, model.Direction);
        Assert.Equal("hello", model.PromptText);
        Assert.Equal(new[] { "xin chào", "tạm biệt", "cảm ơn", "xin lỗi" }, model.Choices);

        string serialized = JsonSerializer.Serialize(model);
        Assert.DoesNotContain("CorrectChoiceIndex", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCorrect", serialized, StringComparison.Ordinal);
        Assert.Null(typeof(QuizStudyViewModel).GetProperty("CorrectAnswer"));
    }

    [Fact]
    public async Task Quiz_requested_answered_question_maps_read_only_review_navigation()
    {
        QuizQuestionState state = new()
        {
            SetId = 7,
            SetTitle = "Core English",
            SessionId = 42,
            TotalQuestions = 10,
            AnsweredCount = 2,
            CorrectCount = 1,
            Question = CreateQuestionState().Question,
            IsReviewOnly = true,
            SelectedChoiceIndex = 0,
            CorrectChoiceIndex = 2,
            IsCorrect = false,
            PreviousQuestionId = 500,
            NextQuestionId = 502,
            CurrentPendingQuestionId = 503
        };
        _quizService.Setup(service => service.GetCurrentQuestionAsync(7, 42, "user-1", 501))
            .ReturnsAsync(state);
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.Quiz(7, 42, 501);

        QuizStudyViewModel model = Assert.IsType<QuizStudyViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.IsReviewOnly);
        Assert.Equal(0, model.SelectedChoiceIndex);
        Assert.Equal(2, model.CorrectChoiceIndex);
        Assert.False(model.IsCorrect);
        Assert.Equal(500, model.PreviousQuestionId);
        Assert.Equal(502, model.NextQuestionId);
        Assert.Equal(503, model.CurrentPendingQuestionId);
        _quizService.Verify(service => service.GetCurrentQuestionAsync(7, 42, "user-1", 501));
    }

    [Fact]
    public async Task Quiz_completed_redirects_to_result()
    {
        _quizService.Setup(service => service.GetCurrentQuestionAsync(7, 42, "user-1", null))
            .ReturnsAsync(new QuizQuestionState
            {
                SetId = 7,
                SessionId = 42,
                TotalQuestions = 10,
                AnsweredCount = 10,
                CorrectCount = 8,
                Question = null
            });
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.Quiz(7, 42);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(StudyController.QuizResult), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["setId"]);
        Assert.Equal(42, redirect.RouteValues["sessionId"]);
    }

    [Fact]
    public async Task Quiz_expired_redirects_to_result()
    {
        _quizService.Setup(service => service.GetCurrentQuestionAsync(7, 42, "user-1", null))
            .ThrowsAsync(new QuizExpiredException());
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.Quiz(7, 42);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(StudyController.QuizResult), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["setId"]);
        Assert.Equal(42, redirect.RouteValues["sessionId"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Quiz_abandoned_redirects_to_replacement_or_setup(bool hasReplacement)
    {
        _quizService.Setup(service => service.GetCurrentQuestionAsync(7, 42, "user-1", null))
            .ThrowsAsync(new QuizSessionAbandonedException(hasReplacement ? 84 : null));
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.Quiz(7, 42);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(hasReplacement ? nameof(StudyController.Quiz) : nameof(StudyController.QuizStart), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["setId"]);
        if (hasReplacement)
        {
            Assert.Equal(84, redirect.RouteValues["sessionId"]);
        }
    }

    [Fact]
    public async Task QuizAnswer_maps_success_json()
    {
        _quizService.Setup(service => service.AnswerAsync(7, 42, 501, 2, "user-1"))
            .ReturnsAsync(new QuizAnswerResult(true, 2, true));
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizAnswer(7, 42, 501, 2);

        JsonElement json = SerializeJsonResult(result);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.True(json.GetProperty("isCorrect").GetBoolean());
        Assert.Equal(2, json.GetProperty("correctChoiceIndex").GetInt32());
        Assert.True(json.GetProperty("isLastQuestion").GetBoolean());
        Assert.Equal("/Study/7/Quiz/Result/42", json.GetProperty("nextUrl").GetString());
    }

    [Fact]
    public async Task QuizTimeout_ajax_completes_expired_attempt_and_returns_result_url()
    {
        StudyController controller = CreateController("user-1");
        controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        IActionResult result = await controller.QuizTimeout(7, 42);

        JsonElement json = SerializeJsonResult(result);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("/Study/7/Quiz/Result/42", json.GetProperty("nextUrl").GetString());
        _quizService.Verify(service => service.CompleteExpiredAsync(7, 42, "user-1"), Times.Once);
    }

    [Fact]
    public async Task QuizTimeout_browser_post_redirects_to_result()
    {
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizTimeout(7, 42);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(StudyController.QuizResult), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["setId"]);
        Assert.Equal(42, redirect.RouteValues["sessionId"]);
    }

    [Fact]
    public async Task QuizTimeout_abandoned_ajax_returns_replacement_navigation()
    {
        _quizService.Setup(service => service.CompleteExpiredAsync(7, 42, "user-1"))
            .ThrowsAsync(new QuizSessionAbandonedException(84));
        StudyController controller = CreateController("user-1");
        controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        IActionResult result = await controller.QuizTimeout(7, 42);

        ObjectResult conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        JsonElement json = JsonSerializer.SerializeToElement(conflict.Value);
        Assert.True(json.GetProperty("stale").GetBoolean());
        Assert.Equal("/Study/7/Quiz/84", json.GetProperty("nextUrl").GetString());
    }

    [Fact]
    public async Task QuizAnswer_expired_returns_result_navigation_json()
    {
        _quizService.Setup(service => service.AnswerAsync(7, 42, 501, 2, "user-1"))
            .ThrowsAsync(new QuizExpiredException());
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizAnswer(7, 42, 501, 2);

        ObjectResult conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        JsonElement json = JsonSerializer.SerializeToElement(conflict.Value);
        Assert.True(json.GetProperty("expired").GetBoolean());
        Assert.Equal("/Study/7/Quiz/Result/42", json.GetProperty("nextUrl").GetString());
    }

    [Fact]
    public async Task QuizAnswer_abandoned_returns_replacement_navigation_json()
    {
        _quizService.Setup(service => service.AnswerAsync(7, 42, 501, 2, "user-1"))
            .ThrowsAsync(new QuizSessionAbandonedException(84));
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizAnswer(7, 42, 501, 2);

        ObjectResult conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        JsonElement json = JsonSerializer.SerializeToElement(conflict.Value);
        Assert.True(json.GetProperty("stale").GetBoolean());
        Assert.Equal("/Study/7/Quiz/84", json.GetProperty("nextUrl").GetString());
    }

    [Fact]
    public async Task QuizTimeout_not_expired_returns_server_remaining_seconds_for_resume()
    {
        _quizService.Setup(service => service.CompleteExpiredAsync(7, 42, "user-1"))
            .ThrowsAsync(new QuizNotExpiredException(37));
        StudyController controller = CreateController("user-1");
        controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        IActionResult result = await controller.QuizTimeout(7, 42);

        ObjectResult conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        JsonElement json = JsonSerializer.SerializeToElement(conflict.Value);
        Assert.False(json.GetProperty("expired").GetBoolean());
        Assert.Equal(37, json.GetProperty("remainingSeconds").GetInt32());
        Assert.Equal("/Study/7/Quiz/42", json.GetProperty("nextUrl").GetString());
    }

    [Theory]
    [InlineData(typeof(ArgumentOutOfRangeException), 400)]
    [InlineData(typeof(UnauthorizedAccessException), 403)]
    [InlineData(typeof(KeyNotFoundException), 404)]
    [InlineData(typeof(QuizConflictException), 409)]
    public async Task QuizAnswer_maps_domain_errors(Type exceptionType, int statusCode)
    {
        Exception exception = CreateDomainException(exceptionType);
        _quizService.Setup(service => service.AnswerAsync(7, 42, 501, 2, "user-1"))
            .ThrowsAsync(exception);
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizAnswer(7, 42, 501, 2);

        Assert.Equal(statusCode, GetStatusCode(result));
        if (statusCode == StatusCodes.Status409Conflict)
        {
            JsonElement json = JsonSerializer.SerializeToElement(
                Assert.IsType<ObjectResult>(result).Value);
            Assert.False(json.GetProperty("success").GetBoolean());
            Assert.Equal(exception.Message, json.GetProperty("message").GetString());
        }
    }

    [Fact]
    public async Task QuizResult_returns_result_view()
    {
        _quizService.Setup(service => service.GetResultAsync(7, 42, "user-1"))
            .ReturnsAsync(new QuizSessionResult
            {
                SetId = 7,
                SetTitle = "Core English",
                SessionId = 42,
                TotalQuestions = 10,
                CorrectCount = 7,
                Score = 70,
                WrongAnswers = new[]
                {
                    new QuizWrongAnswer(
                        9,
                        QuizQuestionDirection.TermToDefinition,
                        "hello",
                        "tạm biệt",
                        "xin chào")
                }
            });
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizResult(7, 42);

        QuizResultViewModel model = Assert.IsType<QuizResultViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(7, model.SetId);
        Assert.Equal("Core English", model.SetTitle);
        Assert.Equal(42, model.SessionId);
        Assert.Equal(10, model.TotalQuestions);
        Assert.Equal(7, model.CorrectCount);
        Assert.Equal(70, model.Score);
        QuizWrongAnswerViewModel wrong = Assert.Single(model.WrongAnswers);
        Assert.Equal(QuizQuestionDirection.TermToDefinition, wrong.Direction);
        Assert.Equal("hello", wrong.PromptText);
        Assert.Equal("tạm biệt", wrong.SelectedAnswer);
        Assert.Equal("xin chào", wrong.CorrectAnswer);
    }

    [Fact]
    public async Task RetryWrong_redirects_to_new_session()
    {
        _quizService.Setup(service => service.RetryWrongAsync(7, 42, "user-1"))
            .ReturnsAsync(new StudySession { Id = 84 });
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.RetryWrong(7, 42);

        AssertQuizSessionRedirect(result, setId: 7, sessionId: 84);
    }

    [Fact]
    public async Task RetryAll_redirects_to_new_session()
    {
        _quizService.Setup(service => service.RetryAllAsync(7, 42, "user-1"))
            .ReturnsAsync(new StudySession { Id = 85 });
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.RetryAll(7, 42);

        AssertQuizSessionRedirect(result, setId: 7, sessionId: 85);
    }

    [Fact]
    public async Task QuizRestart_redirects_to_fresh_session()
    {
        _quizService.Setup(service => service.RestartAsync(7, 42, "user-1"))
            .ReturnsAsync(new StudySession { Id = 86 });
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizRestart(7, 42);

        AssertQuizSessionRedirect(result, setId: 7, sessionId: 86);
    }

    [Fact]
    public async Task QuizRestart_expired_redirects_to_result()
    {
        _quizService.Setup(service => service.RestartAsync(7, 42, "user-1"))
            .ThrowsAsync(new QuizExpiredException());
        StudyController controller = CreateController("user-1");

        IActionResult result = await controller.QuizRestart(7, 42);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(StudyController.QuizResult), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["setId"]);
        Assert.Equal(42, redirect.RouteValues["sessionId"]);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("all")]
    public async Task Retry_unavailable_redirects_to_result_with_feedback(string retryMode)
    {
        const string message = "Thẻ nguồn không còn khả dụng.";
        _quizService.Setup(service => service.RetryWrongAsync(7, 42, "user-1"))
            .ThrowsAsync(new QuizUnavailableException(message));
        _quizService.Setup(service => service.RetryAllAsync(7, 42, "user-1"))
            .ThrowsAsync(new QuizUnavailableException(message));
        StudyController controller = CreateController("user-1");

        IActionResult result = retryMode == "wrong"
            ? await controller.RetryWrong(7, 42)
            : await controller.RetryAll(7, 42);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(StudyController.QuizResult), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["setId"]);
        Assert.Equal(42, redirect.RouteValues["sessionId"]);
        Assert.Equal(message, controller.TempData["Message"]);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("question")]
    [InlineData("result")]
    public async Task Quiz_get_actions_challenge_anonymous_users(string action)
    {
        StudyController controller = CreateController(userId: null);

        IActionResult result = action switch
        {
            "start" => await controller.QuizStart(7),
            "question" => await controller.Quiz(7, 42),
            "result" => await controller.QuizResult(7, 42),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        Assert.IsType<ChallengeResult>(result);
    }

    [Theory]
    [InlineData("answer")]
    [InlineData("timeout")]
    [InlineData("restart")]
    [InlineData("retry-wrong")]
    [InlineData("retry-all")]
    public async Task Quiz_ajax_post_actions_unauthorize_anonymous_users(string action)
    {
        StudyController controller = CreateController(userId: null);
        controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        IActionResult result = action switch
        {
            "answer" => await controller.QuizAnswer(7, 42, 501, 2),
            "timeout" => await controller.QuizTimeout(7, 42),
            "restart" => await controller.QuizRestart(7, 42),
            "retry-wrong" => await controller.RetryWrong(7, 42),
            "retry-all" => await controller.RetryAll(7, 42),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void Quiz_routes_constrain_session_ids_and_post_actions_validate_antiforgery()
    {
        AssertRoute(nameof(StudyController.QuizStart), 1, "/Study/{setId}/Quiz", typeof(HttpGetAttribute));
        AssertRoute(nameof(StudyController.QuizStart), 2, "/Study/{setId}/Quiz/Start", typeof(HttpPostAttribute));
        AssertRoute(nameof(StudyController.Quiz), 3, "/Study/{setId}/Quiz/{sessionId:int}", typeof(HttpGetAttribute));
        AssertRoute(nameof(StudyController.QuizAnswer), 4, "/Study/{setId}/Quiz/{sessionId:int}/Answer", typeof(HttpPostAttribute));
        AssertRoute(nameof(StudyController.QuizTimeout), 2, "/Study/{setId}/Quiz/{sessionId:int}/Timeout", typeof(HttpPostAttribute));
        AssertRoute(nameof(StudyController.QuizRestart), 2, "/Study/{setId}/Quiz/{sessionId:int}/Restart", typeof(HttpPostAttribute));
        AssertRoute(nameof(StudyController.QuizResult), 2, "/Study/{setId}/Quiz/Result/{sessionId:int}", typeof(HttpGetAttribute));
        AssertRoute(nameof(StudyController.RetryWrong), 2, "/Study/{setId}/Quiz/{sessionId:int}/RetryWrong", typeof(HttpPostAttribute));
        AssertRoute(nameof(StudyController.RetryAll), 2, "/Study/{setId}/Quiz/{sessionId:int}/RetryAll", typeof(HttpPostAttribute));

        AssertPostValidatesAntiforgery(nameof(StudyController.QuizAnswer));
        AssertPostValidatesAntiforgery(nameof(StudyController.QuizTimeout));
        AssertPostValidatesAntiforgery(nameof(StudyController.QuizRestart));
        AssertPostValidatesAntiforgery(nameof(StudyController.RetryWrong));
        AssertPostValidatesAntiforgery(nameof(StudyController.RetryAll));
        MethodInfo quizStartPost = typeof(StudyController).GetMethods()
            .Single(candidate => candidate.Name == nameof(StudyController.QuizStart)
                && candidate.GetParameters().Length == 2);
        Assert.NotNull(quizStartPost.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    private StudyController CreateController(string? userId)
    {
        Mock<ICurrentUser> currentUser = new();
        currentUser.Setup(user => user.UserId).Returns(userId);
        currentUser.Setup(user => user.IsAuthenticated).Returns(userId is not null);

        DefaultHttpContext httpContext = new();
        StudyController controller = new(
            _studyService.Object,
            _dictationService.Object,
            _quizService.Object,
            _setService.Object,
            currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(
                httpContext,
                Mock.Of<ITempDataProvider>()),
            Url = new QuizUrlHelper()
        };
        return controller;
    }

    private static QuizQuestionState CreateQuestionState()
    {
        return new QuizQuestionState
        {
            SetId = 7,
            SetTitle = "Core English",
            SessionId = 42,
            TotalQuestions = 10,
            AnsweredCount = 2,
            CorrectCount = 1,
            Question = new QuizSessionQuestion
            {
                Id = 501,
                StudySessionId = 42,
                FlashcardId = 9,
                OrderIndex = 2,
                Direction = QuizQuestionDirection.TermToDefinition,
                PromptText = "hello",
                Choice1Text = "xin chào",
                Choice2Text = "tạm biệt",
                Choice3Text = "cảm ơn",
                Choice4Text = "xin lỗi",
                CorrectChoiceIndex = 2
            }
        };
    }

    private static Exception CreateDomainException(Type exceptionType)
    {
        if (exceptionType == typeof(QuizConflictException))
        {
            return new QuizConflictException("Câu hỏi đã được trả lời.");
        }

        return (Exception)Activator.CreateInstance(exceptionType)!;
    }

    private static int GetStatusCode(IActionResult result)
    {
        return result switch
        {
            BadRequestResult => StatusCodes.Status400BadRequest,
            ForbidResult => StatusCodes.Status403Forbidden,
            NotFoundResult => StatusCodes.Status404NotFound,
            ObjectResult objectResult when objectResult.StatusCode.HasValue => objectResult.StatusCode.Value,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected result type: {result.GetType().Name}")
        };
    }

    private static JsonElement SerializeJsonResult(IActionResult result)
    {
        return JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);
    }

    private static void AssertQuizSessionRedirect(IActionResult result, int setId, int sessionId)
    {
        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(StudyController.Quiz), redirect.ActionName);
        Assert.Equal(setId, redirect.RouteValues!["setId"]);
        Assert.Equal(sessionId, redirect.RouteValues["sessionId"]);
    }

    private static void AssertRoute(
        string methodName,
        int parameterCount,
        string expectedTemplate,
        Type httpMethodAttributeType)
    {
        MethodInfo method = typeof(StudyController).GetMethods()
            .Single(candidate =>
                candidate.Name == methodName
                && candidate.GetParameters().Length == parameterCount);
        RouteAttribute route = Assert.Single(method.GetCustomAttributes<RouteAttribute>());
        Assert.Equal(expectedTemplate, route.Template);
        Assert.NotNull(method.GetCustomAttributes(httpMethodAttributeType, inherit: true).SingleOrDefault());
    }

    private static void AssertPostValidatesAntiforgery(string methodName)
    {
        MethodInfo method = typeof(StudyController).GetMethod(methodName)!;
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    private sealed class QuizUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();

        public string? Action(UrlActionContext actionContext)
        {
            RouteValueDictionary values = new(actionContext.Values);
            int setId = (int)values["setId"]!;
            if (actionContext.Action == nameof(StudyController.QuizStart))
            {
                return $"/Study/{setId}/Quiz";
            }

            int sessionId = (int)values["sessionId"]!;

            return actionContext.Action == nameof(StudyController.QuizResult)
                ? $"/Study/{setId}/Quiz/Result/{sessionId}"
                : $"/Study/{setId}/Quiz/{sessionId}";
        }

        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/";
        public string? RouteUrl(UrlRouteContext routeContext) => "/";
    }
}
