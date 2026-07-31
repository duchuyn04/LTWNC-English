using System.Reflection;
using ltwnc.Controllers;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using ltwnc.Services.Auth;
using ltwnc.Services.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ltwnc.Tests.Controllers;

public sealed class ReviewControllerTests
{
    [Fact]
    public void ReviewController_HasAuthorizeAttribute()
    {
        Assert.NotEmpty(typeof(ReviewController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }

    [Theory]
    [InlineData(nameof(ReviewController.Start))]
    [InlineData(nameof(ReviewController.Rate))]
    [InlineData(nameof(ReviewController.End))]
    public void ReviewController_WriteAction_HasPostAndAntiforgery(string actionName)
    {
        MethodInfo? method = actionName == nameof(ReviewController.Start)
            ? typeof(ReviewController).GetMethod(actionName, new[] { typeof(int) })
            : typeof(ReviewController).GetMethod(actionName);

        Assert.NotNull(method);
        Assert.NotEmpty(method!.GetCustomAttributes<HttpPostAttribute>(inherit: true));
        Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>(inherit: true));
    }

    [Fact]
    public async Task Index_AnonymousUser_ReturnsChallenge()
    {
        var review = new Mock<IReviewService>();
        ReviewController controller = CreateController(null, review);

        IActionResult actual = await controller.Index();

        Assert.IsType<ChallengeResult>(actual);
        review.Verify(service => service.GetActiveSessionAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Index_ActiveSession_RedirectsToSession()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetActiveSessionAsync("user-1"))
            .ReturnsAsync(new ReviewSessionViewModel { SessionId = 17 });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Session), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["sessionId"]);
    }

    [Fact]
    public async Task Index_WithoutActiveSession_RedirectsToSetPicker()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetActiveSessionAsync("user-1"))
            .ReturnsAsync((ReviewSessionViewModel?)null);
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Index();

        Assert.Equal("/Set", Assert.IsType<RedirectResult>(actual).Url);
    }

    [Fact]
    public async Task Start_AnonymousUser_ReturnsUnauthorized()
    {
        var review = new Mock<IReviewService>();
        ReviewController controller = CreateController(null, review);

        IActionResult actual = await controller.Start();

        Assert.IsType<UnauthorizedResult>(actual);
        review.Verify(service => service.StartAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Start_AuthenticatedUser_RedirectsToSetPicker()
    {
        var review = new Mock<IReviewService>();
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Start();

        Assert.Equal("/Set", Assert.IsType<RedirectResult>(actual).Url);
        Assert.Equal(
            "H\u00e3y ch\u1ecdn m\u1ed9t b\u1ed9 th\u1ebb \u0111\u1ec3 b\u1eaft \u0111\u1ea7u Review.",
            controller.TempData["Message"]);
        review.Verify(service => service.StartAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Start_WithSetId_CreatedSession_RedirectsToSession()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSetAsync("user-1", 5))
            .ReturnsAsync(new ReviewSetViewModel { SetId = 5 });
        review.Setup(service => service.StartAsync("user-1", 5))
            .ReturnsAsync(new ReviewSessionViewModel { SessionId = 17 });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Start(5);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Session), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["sessionId"]);
    }

    [Fact]
    public async Task Start_WithSetId_UnknownSet_ReturnsNotFound()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSetAsync("user-1", 5))
            .ReturnsAsync((ReviewSetViewModel?)null);
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Start(5);

        Assert.IsType<NotFoundResult>(actual);
        review.Verify(service => service.StartAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Start_WithSetId_NoEligibleCards_RedirectsToSetWithMessage()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSetAsync("user-1", 5))
            .ReturnsAsync(new ReviewSetViewModel { SetId = 5, IsPaused = false });
        review.Setup(service => service.StartAsync("user-1", 5))
            .ReturnsAsync((ReviewSessionViewModel?)null);
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Start(5);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Set), redirect.ActionName);
        Assert.Equal(5, redirect.RouteValues!["setId"]);
        Assert.Equal(
            "Ch\u01b0a c\u00f3 th\u1ebb ph\u00f9 h\u1ee3p \u0111\u1ec3 b\u1eaft \u0111\u1ea7u \u00f4n t\u1eadp.",
            controller.TempData["Message"]);
    }

    [Fact]
    public async Task Start_WithSetId_PausedSet_ShowsPausedMessage()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSetAsync("user-1", 5))
            .ReturnsAsync(new ReviewSetViewModel { SetId = 5, IsPaused = true });
        review.Setup(service => service.StartAsync("user-1", 5))
            .ReturnsAsync((ReviewSessionViewModel?)null);
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Start(5);

        Assert.Equal(
            "B\u1ed9 th\u1ebb \u0111ang t\u1ea1m d\u1eebng Review.",
            controller.TempData["Message"]);
    }

    [Fact]
    public async Task Start_WithSetId_AnonymousUser_ReturnsUnauthorized()
    {
        var review = new Mock<IReviewService>();
        ReviewController controller = CreateController(null, review);

        IActionResult actual = await controller.Start(5);

        Assert.IsType<UnauthorizedResult>(actual);
        review.Verify(service => service.GetSetAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Set_ExistingSet_ReturnsIndexViewWithModel()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSetAsync("user-1", 5))
            .ReturnsAsync(new ReviewSetViewModel { SetId = 5, SetTitle = "Everyday English" });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Set(5);

        var view = Assert.IsType<ViewResult>(actual);
        Assert.Equal("Index", view.ViewName);
        Assert.Equal(5, Assert.IsType<ReviewSetViewModel>(view.Model).SetId);
    }

    [Fact]
    public async Task Set_UnknownSet_ReturnsNotFound()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSetAsync("user-1", 5))
            .ReturnsAsync((ReviewSetViewModel?)null);
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Set(5);

        Assert.IsType<NotFoundResult>(actual);
    }

    [Fact]
    public async Task Set_AnonymousUser_ReturnsChallenge()
    {
        var review = new Mock<IReviewService>();
        ReviewController controller = CreateController(null, review);

        IActionResult actual = await controller.Set(5);

        Assert.IsType<ChallengeResult>(actual);
        review.Verify(service => service.GetSetAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Session_ExistingSession_ReturnsReviewViewModel()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSessionAsync(17, "user-1"))
            .ReturnsAsync(new ReviewSessionViewModel
            {
                SessionId = 17,
                TotalCards = 1,
                Cards = new[]
                {
                    new ReviewCardViewModel
                    {
                        FlashcardId = 3,
                        FrontText = "hello",
                        BackText = "xin chào"
                    }
                }
            });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Session(17);

        var view = Assert.IsType<ViewResult>(actual);
        var model = Assert.IsType<ReviewSessionViewModel>(view.Model);
        Assert.Equal("hello", Assert.Single(model.Cards).FrontText);
    }

    [Fact]
    public async Task Session_CompletedSession_RedirectsToResult()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSessionAsync(17, "user-1"))
            .ReturnsAsync(new ReviewSessionViewModel { SessionId = 17, IsCompleted = true });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Session(17);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Result), redirect.ActionName);
    }

    [Fact]
    public async Task Session_AnonymousUser_ReturnsChallenge()
    {
        var review = new Mock<IReviewService>();
        ReviewController controller = CreateController(null, review);

        IActionResult actual = await controller.Session(17);

        Assert.IsType<ChallengeResult>(actual);
        review.Verify(service => service.GetSessionAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Session_MissingSession_ReturnsNotFound()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSessionAsync(17, "user-1"))
            .ReturnsAsync((ReviewSessionViewModel?)null);
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Session(17);

        Assert.IsType<NotFoundResult>(actual);
    }

    [Fact]
    public async Task Result_EndedSession_ReturnsSummaryView()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSessionAsync(17, "user-1"))
            .ReturnsAsync(new ReviewSessionViewModel { SessionId = 17, IsEnded = true });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Result(17);

        var view = Assert.IsType<ViewResult>(actual);
        Assert.True(Assert.IsType<ReviewSessionViewModel>(view.Model).IsEnded);
    }

    [Fact]
    public async Task Rate_WithoutAnswerReveal_ReturnsConflict()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.RateAsync(
                "user-1", 17, 3, ReviewRating.Good, false))
            .ThrowsAsync(new InvalidOperationException("Bạn cần hiện đáp án trước khi chọn mức nhớ."));
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Rate(17, 3, ReviewRating.Good, false);

        var conflict = Assert.IsType<ConflictObjectResult>(actual);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Rate_AnonymousUser_ReturnsUnauthorized()
    {
        var review = new Mock<IReviewService>();
        ReviewController controller = CreateController(null, review);

        IActionResult actual = await controller.Rate(17, 3, ReviewRating.Good, true);

        Assert.IsType<UnauthorizedResult>(actual);
        review.Verify(
            service => service.RateAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<ReviewRating>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task Rate_UnknownSession_ReturnsNotFound()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.RateAsync("user-1", 17, 3, ReviewRating.Good, true))
            .ThrowsAsync(new KeyNotFoundException());
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Rate(17, 3, ReviewRating.Good, true);

        Assert.IsType<NotFoundResult>(actual);
    }

    [Fact]
    public async Task Rate_AuthenticatedUser_RedirectsToResult()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.RateAsync(
                "user-1", 17, 3, ReviewRating.Good, true))
            .ReturnsAsync(new ReviewRatingResult
            {
                Session = new ReviewSessionViewModel { SessionId = 17, IsCompleted = true },
                Progress = new ReviewProgressViewModel { Stage = ReviewStage.Reviewing }
            });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Rate(17, 3, ReviewRating.Good, true);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Result), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["sessionId"]);
    }

    [Fact]
    public async Task Rate_WhileSessionHasCardsRemaining_RedirectsBackToSession()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.RateAsync(
                "user-1", 17, 3, ReviewRating.Good, true))
            .ReturnsAsync(new ReviewRatingResult
            {
                Session = new ReviewSessionViewModel { SessionId = 17, IsCompleted = false },
                Progress = new ReviewProgressViewModel { Stage = ReviewStage.Reviewing }
            });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Rate(17, 3, ReviewRating.Good, true);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Session), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["sessionId"]);
    }

    [Fact]
    public async Task End_ExistingSession_RedirectsToResult()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.EndAsync("user-1", 17))
            .ReturnsAsync(new ReviewSessionViewModel { SessionId = 17, IsEnded = true });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.End(17);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Result), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["sessionId"]);
    }

    [Fact]
    public async Task End_AnonymousUser_ReturnsUnauthorized()
    {
        var review = new Mock<IReviewService>();
        ReviewController controller = CreateController(null, review);

        IActionResult actual = await controller.End(17);

        Assert.IsType<UnauthorizedResult>(actual);
        review.Verify(service => service.EndAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task End_MissingSession_ReturnsNotFound()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.EndAsync("user-1", 17))
            .ReturnsAsync((ReviewSessionViewModel?)null);
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.End(17);

        Assert.IsType<NotFoundResult>(actual);
    }

    [Fact]
    public async Task Result_AnonymousUser_ReturnsChallenge()
    {
        var review = new Mock<IReviewService>();
        ReviewController controller = CreateController(null, review);

        IActionResult actual = await controller.Result(17);

        Assert.IsType<ChallengeResult>(actual);
        review.Verify(service => service.GetSessionAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Result_MissingSession_ReturnsNotFound()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSessionAsync(17, "user-1"))
            .ReturnsAsync((ReviewSessionViewModel?)null);
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Result(17);

        Assert.IsType<NotFoundResult>(actual);
    }

    [Fact]
    public async Task Result_UnfinishedSession_RedirectsToSession()
    {
        var review = new Mock<IReviewService>();
        review.Setup(service => service.GetSessionAsync(17, "user-1"))
            .ReturnsAsync(new ReviewSessionViewModel { SessionId = 17 });
        ReviewController controller = CreateController("user-1", review);

        IActionResult actual = await controller.Result(17);

        var redirect = Assert.IsType<RedirectToActionResult>(actual);
        Assert.Equal(nameof(ReviewController.Session), redirect.ActionName);
        Assert.Equal(17, redirect.RouteValues!["sessionId"]);
    }

    private static ReviewController CreateController(
        string? userId,
        Mock<IReviewService> review)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(value => value.UserId).Returns(userId);
        return new ReviewController(review.Object, currentUser.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>())
        };
    }
}
