using ltwnc.Controllers;
using ltwnc.Models.ViewModels.Review;
using ltwnc.Services.Auth;
using ltwnc.Services.Review;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ltwnc.Tests.Controllers;

public sealed class ReviewSettingsControllerTests
{
    [Fact]
    public async Task Settings_OwnedSet_ReturnsSavedOrDefaultSettings()
    {
        Mock<IReviewSettingsService> settings = new();
        settings.Setup(service => service.GetOrCreateAsync("user-1", 7, default))
            .ReturnsAsync(new ReviewSettingsViewModel());
        ReviewController controller = CreateController("user-1", settings);

        IActionResult actual = await controller.Settings(7);

        Assert.IsType<OkObjectResult>(actual);
        settings.Verify(service => service.GetOrCreateAsync("user-1", 7, default), Times.Once);
    }

    [Fact]
    public async Task Settings_ForeignOrUnknownSet_ReturnsNotFound()
    {
        Mock<IReviewSettingsService> settings = new();
        settings.Setup(service => service.GetOrCreateAsync("user-1", 7, default))
            .ReturnsAsync((ReviewSettingsViewModel?)null);
        ReviewController controller = CreateController("user-1", settings);

        IActionResult actual = await controller.Settings(7);

        Assert.IsType<NotFoundResult>(actual);
    }

    [Fact]
    public async Task Settings_AnonymousUser_ReturnsChallenge()
    {
        ReviewController controller = CreateController(null, new Mock<IReviewSettingsService>());

        IActionResult actual = await controller.Settings(7);

        Assert.IsType<ChallengeResult>(actual);
    }

    [Fact]
    public async Task SaveSettings_InvalidRange_ReturnsBadRequestWithoutPersistence()
    {
        Mock<IReviewSettingsService> settings = new();
        ReviewController controller = CreateController("user-1", settings);
        controller.ModelState.AddModelError(nameof(ReviewSettingsViewModel.NewCardQuota), "Ngoài miền");

        IActionResult actual = await controller.SaveSettings(7, new ReviewSettingsViewModel());

        Assert.IsType<BadRequestObjectResult>(actual);
        settings.Verify(
            service => service.SaveAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<ReviewSettingsViewModel>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveSettings_OwnedSet_ReturnsPersistedSettings()
    {
        Mock<IReviewSettingsService> settings = new();
        settings.Setup(service => service.SaveAsync(
                "user-1",
                7,
                It.IsAny<ReviewSettingsViewModel>(),
                default))
            .ReturnsAsync(new ReviewSettingsViewModel { ReviewSessionSize = 50 });
        ReviewController controller = CreateController("user-1", settings);

        IActionResult actual = await controller.SaveSettings(
            7,
            new ReviewSettingsViewModel { ReviewSessionSize = 50 });

        OkObjectResult result = Assert.IsType<OkObjectResult>(actual);
        Assert.Equal(50, ((ReviewSettingsViewModel)result.Value!).ReviewSessionSize);
    }

    [Fact]
    public async Task SaveSettings_AnonymousUser_ReturnsUnauthorized()
    {
        ReviewController controller = CreateController(null, new Mock<IReviewSettingsService>());

        IActionResult actual = await controller.SaveSettings(7, new ReviewSettingsViewModel());

        Assert.IsType<UnauthorizedResult>(actual);
    }

    [Fact]
    public void SaveSettings_RequiresPostAndAntiforgery()
    {
        var method = typeof(ReviewController).GetMethod(nameof(ReviewController.SaveSettings));

        Assert.NotNull(method);
        Assert.NotEmpty(method!.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true));
        Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true));
    }

    private static ReviewController CreateController(
        string? userId,
        Mock<IReviewSettingsService> settings)
    {
        Mock<ICurrentUser> currentUser = new();
        currentUser.SetupGet(value => value.UserId).Returns(userId);
        return new ReviewController(
            Mock.Of<IReviewService>(),
            currentUser.Object,
            settings.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
