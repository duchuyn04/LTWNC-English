using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Services.Auth;
using ltwnc.Services.EnglishMission;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using EnglishMissionEntity = ltwnc.Models.Entities.EnglishMission;

namespace ltwnc.Tests.Controllers;

public sealed class EnglishMissionControllerTests
{
    [Fact]
    public async Task Result_incomplete_mission_redirects_to_chat_with_feedback()
    {
        var missionService = new Mock<IEnglishMissionService>();
        var setService = new Mock<IFlashcardSetService>();
        missionService.Setup(service => service.GetAsync("user-1", 7, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResult("Active"));
        setService.Setup(service => service.GetOwnedSetAsync(7, "user-1"))
            .ReturnsAsync(new FlashcardSet { Id = 7, Title = "Travel" });
        EnglishMissionController controller = CreateController(
            missionService.Object,
            setService.Object,
            "user-1");

        IActionResult result = await controller.Result(7, 42, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(EnglishMissionController.Chat), redirect.ActionName);
        Assert.Equal(7, redirect.RouteValues!["setId"]);
        Assert.Equal(42, redirect.RouteValues["sessionId"]);
        Assert.Equal(
            "Mission chưa hoàn thành. Hãy tiếp tục hội thoại để xem kết quả.",
            controller.TempData["MissionError"]);
    }

    [Fact]
    public async Task Chat_missing_mission_returns_not_found()
    {
        var missionService = new Mock<IEnglishMissionService>();
        var setService = new Mock<IFlashcardSetService>();
        missionService.Setup(service => service.GetAsync("user-1", 7, 42, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());
        EnglishMissionController controller = CreateController(
            missionService.Object,
            setService.Object,
            "user-1");

        IActionResult result = await controller.Chat(7, 42, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Respond_missing_mission_returns_not_found()
    {
        var missionService = new Mock<IEnglishMissionService>();
        var setService = new Mock<IFlashcardSetService>();
        missionService.Setup(service => service.RespondAsync(
                "user-1",
                7,
                42,
                "turn-1",
                "Hello",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());
        EnglishMissionController controller = CreateController(
            missionService.Object,
            setService.Object,
            "user-1");

        IActionResult result = await controller.Respond(
            7,
            42,
            "turn-1",
            "Hello",
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static EnglishMissionController CreateController(
        IEnglishMissionService missionService,
        IFlashcardSetService setService,
        string? userId)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(user => user.UserId).Returns(userId);

        DefaultHttpContext httpContext = new();
        return new EnglishMissionController(
            missionService,
            setService,
            currentUser.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(
                httpContext,
                Mock.Of<ITempDataProvider>())
        };
    }

    private static EnglishMissionStartResult CreateResult(string status)
    {
        return new EnglishMissionStartResult
        {
            Mission = new EnglishMissionEntity
            {
                StudySessionId = 42,
                Status = status,
                Title = "Travel mission"
            },
            TargetWords = [],
            Turns = []
        };
    }
}
