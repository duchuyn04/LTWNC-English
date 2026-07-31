using System.Reflection;
using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Services.Auth;
using ltwnc.Services.ContentReports;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ltwnc.Tests.Controllers;

public sealed class FlashcardSetDuplicateControllerTests
{
    [Fact]
    public void Duplicate_HasPostAndAntiforgeryAttributes()
    {
        MethodInfo method = typeof(FlashcardSetController)
            .GetMethod(nameof(FlashcardSetController.Duplicate))!;

        Assert.NotEmpty(method.GetCustomAttributes<HttpPostAttribute>(inherit: true));
        Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>(inherit: true));
    }

    [Fact]
    public async Task Duplicate_AnonymousUserReturnsChallenge()
    {
        Mock<IFlashcardSetService> sets = new();
        FlashcardSetController controller = CreateController(null, sets);

        IActionResult result = await controller.Duplicate(10);

        Assert.IsType<ChallengeResult>(result);
        sets.Verify(
            service => service.DuplicateOwnedSetAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Duplicate_SuccessRedirectsToDuplicateDetails()
    {
        Mock<IFlashcardSetService> sets = new();
        sets.Setup(service => service.DuplicateOwnedSetAsync(
                10,
                "user-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlashcardSet
            {
                Id = 42,
                Title = "Vocabulary (Bản sao)"
            });
        FlashcardSetController controller = CreateController("user-1", sets);

        IActionResult result = await controller.Duplicate(10);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(FlashcardSetController.Details), redirect.ActionName);
        Assert.Equal(42, redirect.RouteValues!["id"]);
        Assert.Contains("Vocabulary (Bản sao)", Assert.IsType<string>(controller.TempData["Success"]));
    }

    [Fact]
    public async Task Duplicate_MissingOrForeignSetReturnsNotFound()
    {
        Mock<IFlashcardSetService> sets = new();
        sets.Setup(service => service.DuplicateOwnedSetAsync(
                10,
                "user-1",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());
        FlashcardSetController controller = CreateController("user-1", sets);

        IActionResult result = await controller.Duplicate(10);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Duplicate_BusinessFailureRedirectsBackWithMessage()
    {
        Mock<IFlashcardSetService> sets = new();
        sets.Setup(service => service.DuplicateOwnedSetAsync(
                10,
                "user-1",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Không thể nhân bản bộ thẻ đang bị cách ly."));
        FlashcardSetController controller = CreateController("user-1", sets);

        IActionResult result = await controller.Duplicate(10);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(FlashcardSetController.Details), redirect.ActionName);
        Assert.Equal(10, redirect.RouteValues!["id"]);
        Assert.Contains("cách ly", Assert.IsType<string>(controller.TempData["DuplicateError"]));
    }

    private static FlashcardSetController CreateController(
        string? userId,
        Mock<IFlashcardSetService> sets)
    {
        Mock<ICurrentUser> currentUser = new();
        currentUser.SetupGet(value => value.UserId).Returns(userId);
        FlashcardSetController controller = new(
            sets.Object,
            currentUser.Object,
            new Mock<IFlashcardImportService>().Object,
            new Mock<IContentReportService>().Object);
        DefaultHttpContext httpContext = new();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(
            httpContext,
            new Mock<ITempDataProvider>().Object);
        return controller;
    }
}
