using ltwnc.Controllers;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Mvc;

namespace ltwnc.Tests.Controllers;

public class FlashcardSetStarControllerTests
{
    private static (FlashcardSetController Controller, Mock<IFlashcardSetService> Service) Create(string? userId)
    {
        var service = new Mock<IFlashcardSetService>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(x => x.UserId).Returns(userId);
        currentUser.Setup(x => x.IsAuthenticated).Returns(userId is not null);
        var controller = new FlashcardSetController(
            service.Object,
            currentUser.Object,
            Mock.Of<IFlashcardImportService>(),
            Mock.Of<ltwnc.Services.ContentReports.IContentReportService>());
        return (controller, service);
    }

    [Fact]
    public async Task ToggleStar_owner_returns_json_without_redirect()
    {
        var (controller, service) = Create("user-1");
        service.Setup(x => x.ToggleStarAsync(7, "user-1")).ReturnsAsync(true);

        IActionResult result = await controller.ToggleStar(3, 7);

        var json = Assert.IsType<JsonResult>(result);
        var value = json.Value!;
        Assert.Equal(true, value.GetType().GetProperty("success")!.GetValue(value));
        Assert.Equal(true, value.GetType().GetProperty("isStarred")!.GetValue(value));
        service.Verify(x => x.ToggleStarAsync(7, "user-1"), Times.Once);
    }

    [Fact]
    public async Task ToggleStar_anonymous_returns_challenge_without_calling_service()
    {
        var (controller, service) = Create(null);

        IActionResult result = await controller.ToggleStar(3, 7);

        Assert.IsType<ChallengeResult>(result);
        service.Verify(x => x.ToggleStarAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ToggleStar_missing_card_returns_not_found()
    {
        var (controller, service) = Create("user-1");
        service.Setup(x => x.ToggleStarAsync(7, "user-1"))
            .ThrowsAsync(new KeyNotFoundException());

        IActionResult result = await controller.ToggleStar(3, 7);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ToggleStar_non_owner_returns_forbid()
    {
        var (controller, service) = Create("user-1");
        service.Setup(x => x.ToggleStarAsync(7, "user-1"))
            .ThrowsAsync(new UnauthorizedAccessException());

        IActionResult result = await controller.ToggleStar(3, 7);

        Assert.IsType<ForbidResult>(result);
    }
}
