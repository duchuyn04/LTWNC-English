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

namespace ltwnc.Tests.Controllers;

public sealed class StudyControllerDictationTests
{
    [Fact]
    public async Task DictationCheck_Unauthenticated_Returns401()
    {
        StudyController controller = CreateController(userId: null);

        IActionResult actual = await controller.DictationCheck(1, 2, 3, "answer");

        Assert.IsType<UnauthorizedResult>(actual);
    }

    [Fact]
    public async Task DictationCheck_UnknownSessionOrCard_Returns404()
    {
        var dictation = new Mock<IDictationService>();
        dictation.Setup(service => service.CheckAnswerAsync(2, 1, 3, "answer", "user-1", true))
            .ThrowsAsync(new KeyNotFoundException());
        StudyController controller = CreateController("user-1", dictation: dictation);

        IActionResult actual = await controller.DictationCheck(1, 2, 3, "answer");

        Assert.IsType<NotFoundResult>(actual);
    }

    [Fact]
    public async Task DictationCheck_ForbiddenSession_Returns403()
    {
        var dictation = new Mock<IDictationService>();
        dictation.Setup(service => service.CheckAnswerAsync(2, 1, 3, "answer", "user-1", true))
            .ThrowsAsync(new UnauthorizedAccessException());
        StudyController controller = CreateController("user-1", dictation: dictation);

        IActionResult actual = await controller.DictationCheck(1, 2, 3, "answer");

        Assert.IsType<ForbidResult>(actual);
    }

    [Fact]
    public async Task DictationCheck_CompletedSession_Returns409()
    {
        var dictation = new Mock<IDictationService>();
        dictation.Setup(service => service.CheckAnswerAsync(2, 1, 3, "answer", "user-1", true))
            .ThrowsAsync(new InvalidOperationException("Phiên nghe chép đã hoàn thành."));
        StudyController controller = CreateController("user-1", dictation: dictation);

        IActionResult actual = await controller.DictationCheck(1, 2, 3, "answer");

        var conflict = Assert.IsType<ObjectResult>(actual);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        using JsonDocument json = ToJsonDocument(conflict.Value);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Phiên nghe chép đã hoàn thành.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task DictationCheck_ValidAnswer_ReturnsExpectedJsonContract()
    {
        var dictation = new Mock<IDictationService>();
        dictation.Setup(service => service.CheckAnswerAsync(2, 1, 3, "I like fruit", "user-1", true))
            .ReturnsAsync(new DictationCheckResult
            {
                IsCorrect = false,
                CorrectAnswer = "I like apples",
                Hint = "Nghĩa: Tôi thích táo",
                ExampleMeaning = "Tôi thích táo",
                WordComparison =
                {
                    new DictationWordComparison
                    {
                        Status = DictationWordStatus.Incorrect,
                        AnsweredWord = "fruit",
                        CorrectWord = "apples"
                    }
                }
            });
        StudyController controller = CreateController("user-1", dictation: dictation);

        IActionResult actual = await controller.DictationCheck(1, 2, 3, "I like fruit");

        var result = Assert.IsType<JsonResult>(actual);
        using JsonDocument json = ToJsonDocument(result.Value);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.False(json.RootElement.GetProperty("isCorrect").GetBoolean());
        Assert.Equal("I like apples", json.RootElement.GetProperty("correctAnswer").GetString());
        JsonElement comparison = Assert.Single(json.RootElement.GetProperty("wordComparison").EnumerateArray());
        Assert.Equal("Incorrect", comparison.GetProperty("status").GetString());
        Assert.Equal("fruit", comparison.GetProperty("answeredWord").GetString());
        Assert.Equal("apples", comparison.GetProperty("correctWord").GetString());
    }

    [Fact]
    public async Task DictationComplete_Unauthenticated_Returns401()
    {
        StudyController controller = CreateController(userId: null);

        IActionResult actual = await controller.DictationComplete(1, 2);

        Assert.IsType<UnauthorizedResult>(actual);
    }

    [Fact]
    public async Task DictationComplete_UnansweredQuestions_Returns409()
    {
        var dictation = new Mock<IDictationService>();
        dictation.Setup(service => service.CompleteSessionAsync(2, 1, "user-1"))
            .ThrowsAsync(new InvalidOperationException("Bạn cần hoàn thành tất cả câu hỏi."));
        StudyController controller = CreateController("user-1", dictation: dictation);

        IActionResult actual = await controller.DictationComplete(1, 2);

        var conflict = Assert.IsType<ObjectResult>(actual);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task DictationComplete_CompletedSuccessfully_ReturnsResultRedirectUrl()
    {
        var dictation = new Mock<IDictationService>();
        dictation.Setup(service => service.CompleteSessionAsync(2, 1, "user-1"))
            .ReturnsAsync(new StudySession { Id = 2 });
        StudyController controller = CreateController("user-1", dictation: dictation);

        IActionResult actual = await controller.DictationComplete(1, 2);

        var result = Assert.IsType<JsonResult>(actual);
        using JsonDocument json = ToJsonDocument(result.Value);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("/Study/1/Dictation/Result/2", json.RootElement.GetProperty("redirectUrl").GetString());
    }

    [Fact]
    public async Task Dictation_ExampleSentenceModeWithoutSentences_RedirectsWithMissingSentenceMessage()
    {
        var study = new Mock<IStudyService>();
        study.Setup(service => service.GetSettingsAsync("user-1"))
            .ReturnsAsync(new UserStudySettings
            {
                DictationContentMode = DictationContentMode.ExampleSentence
            });
        var dictation = new Mock<IDictationService>();
        dictation.Setup(service => service.GetCardsForDictationAsync(
                1, "user-1", It.IsAny<UserStudySettings>()))
            .ReturnsAsync(new List<Flashcard>());
        dictation.Setup(service => service.AnyCardHasExampleSentenceAsync(1)).ReturnsAsync(false);
        StudyController controller = CreateController("user-1", study, dictation);

        IActionResult actual = await controller.Dictation(1);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Bộ thẻ chưa có câu ví dụ để nghe chép.", controller.TempData["Message"]);
    }

    [Fact]
    public async Task Dictation_FilterRemovesAllCards_RedirectsWithFilterMessage()
    {
        var study = new Mock<IStudyService>();
        study.Setup(service => service.GetSettingsAsync("user-1"))
            .ReturnsAsync(new UserStudySettings { StarredOnly = true });
        var dictation = new Mock<IDictationService>();
        dictation.Setup(service => service.GetCardsForDictationAsync(
                1, "user-1", It.IsAny<UserStudySettings>()))
            .ReturnsAsync(new List<Flashcard>());
        StudyController controller = CreateController("user-1", study, dictation);

        IActionResult actual = await controller.Dictation(1);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Không có thẻ phù hợp với bộ lọc hiện tại.", controller.TempData["Message"]);
    }

    [Fact]
    public async Task Dictation_RetryHasNoAvailableWrongCards_RedirectsToSourceResult()
    {
        var dictation = new Mock<IDictationService>();
        dictation.Setup(service => service.GetRetryPlanAsync(9, 1, "user-1"))
            .ReturnsAsync(new DictationRetryPlan());
        StudyController controller = CreateController("user-1", dictation: dictation);

        IActionResult actual = await controller.Dictation(1, retrySessionId: 9);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal("DictationResult", redirect.ActionName);
        Assert.Equal("Không còn thẻ sai khả dụng để ôn lại.", controller.TempData["Message"]);
    }

    [Fact]
    public async Task Dictation_ValidCards_CreatesSessionAndMapsUploadedImageFirst()
    {
        var card = new Flashcard
        {
            Id = 3,
            FrontText = "hello",
            BackText = "xin chào",
            Pronunciation = "/həˈləʊ/",
            ExampleSentence = "Hello there.",
            ExampleMeaning = "Xin chào.",
            ImageUrl = "https://example.test/external.png",
            UploadedImagePath = "/uploads/local.png"
        };
        var study = new Mock<IStudyService>();
        study.Setup(service => service.GetSettingsAsync("user-1")).ReturnsAsync(new UserStudySettings());
        var dictation = new Mock<IDictationService>();
        dictation.Setup(service => service.GetCardsForDictationAsync(
                1, "user-1", It.IsAny<UserStudySettings>()))
            .ReturnsAsync(new List<Flashcard> { card });
        dictation.Setup(service => service.CreateSessionAsync(
                "user-1", 1, DictationContentMode.Vocabulary, 1,
                It.Is<IReadOnlyList<Flashcard>>(cards => cards.Count == 1 && cards[0].Id == 3)))
            .ReturnsAsync(new StudySession
            {
                Id = 7,
                DictationContentMode = DictationContentMode.Vocabulary
            });
        StudyController controller = CreateController("user-1", study, dictation);

        IActionResult actual = await controller.Dictation(1);

        var view = Assert.IsType<ViewResult>(actual);
        var model = Assert.IsType<DictationStudyViewModel>(view.Model);
        Assert.Equal(7, model.SessionId);
        Assert.Equal("/uploads/local.png", Assert.Single(model.Cards).ImageUrl);
    }

    private static StudyController CreateController(
        string? userId,
        Mock<IStudyService>? study = null,
        Mock<IDictationService>? dictation = null)
    {
        if (study == null)
        {
            study = new Mock<IStudyService>();
            study.Setup(service => service.GetSettingsAsync(userId)).ReturnsAsync(new UserStudySettings());
        }

        dictation ??= new Mock<IDictationService>();
        var sets = new Mock<IFlashcardSetService>();
        sets.Setup(service => service.GetOwnedSetAsync(1, "user-1"))
            .ReturnsAsync(new FlashcardSet { Id = 1, UserId = "user-1", Title = "Set" });
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(value => value.UserId).Returns(userId);

        var controller = new StudyController(
            study.Object,
            dictation.Object,
            Mock.Of<IQuizService>(),
            sets.Object,
            currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>()),
            Url = new FakeUrlHelper()
        };
        return controller;
    }

    private static JsonDocument ToJsonDocument(object? value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value));

    private sealed class FakeUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext actionContext) => "/Study/1/Dictation/Result/2";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/";
        public string? RouteUrl(UrlRouteContext routeContext) => "/";
    }
}
